namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    /// <inheritdoc/>
    public sealed class BackupManager : IBackupManager
    {
        private readonly IHistoryManager historyManager;
        private readonly IFavoriteManager favoriteManager;
        private readonly ISettingService settingService;

        public BackupManager(
            IHistoryManager historyManager,
            IFavoriteManager favoriteManager,
            ISettingService settingService)
        {
            this.historyManager = historyManager;
            this.favoriteManager = favoriteManager;
            this.settingService = settingService;
        }

        /// <inheritdoc/>
        public int Version => 1;

        /// <inheritdoc/>
        public async Task<BackupData> BackupAsync(BackupDataTypes backupDataType, CancellationToken cancellationToken)
        {
            var backupData = new BackupData();
            backupData.Version = Version;

            var settingsContainers = GetSettingsContainers(backupDataType).ToList();
            if (settingsContainers.Count > 0)
            {
                backupData.Settings = Enum.GetValues(typeof(SettingStrategy))
                    .Cast<SettingStrategy>()
                    .SelectMany(s => settingsContainers.Select(c => (s, c)))
                    .Select(tuple => (
                        tuple.s,
                        tuple.c,
                        i: (IDictionary<string, object>)settingService.GetAllRawSettings(tuple.c, tuple.s)))
                    .Where(tuple => tuple.i.Count > 0)
                    .GroupBy(tuple => tuple.s)
                    .Select(g => (s: g.Key, c: (IDictionary<string, IDictionary<string, object>>)g.ToDictionary(k => k.c, v => v.i)))
                    .ToDictionary(k => k.s, v => v.c);
            }

            var items = Enumerable.Empty<ItemInfo>();

            if (backupDataType.HasFlag(BackupDataTypes.Favorites))
            {
                var favItems = await favoriteManager
                    .AvailableListKinds
                    .ToAsyncEnumerable()
                    .SelectAwaitWithCancellation(async (kind, ct) => (
                        kind: kind,
                        items: await favoriteManager.GetFavorites(kind).ToArrayAsync(ct).ConfigureAwait(false)))
                    .SelectMany(t => t.items.Select(i => (t.kind, item: i)).ToAsyncEnumerable())
                    .Where(t => t.item?.ItemInfo.SiteId != null)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                items = items.Concat(favItems.Select(i => i.item.ItemInfo));

                backupData.Favorites = favItems
                    .Select(i => new FavoriteBackupData
                    {
                        Id = i.item.ItemInfo.SiteId,
                        Site = i.item.ItemInfo.Site,
                        KindNameId = i.kind.ToString()
                    })
                    .ToList();
            }

            if (backupDataType.HasFlag(BackupDataTypes.History))
            {
                var history = await historyManager.GetHistory(true)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

                items = items.Concat(history.Select(i => i.ItemInfo));

                backupData.History = history
                    .Select(h => new HistoryBackupData
                    {
                        AddDateTime = h.AddTime,
                        Id = h.ItemInfo.SiteId,
                        Site = h.ItemInfo.Site,
                        Episode = h.Episode,
                        Season = h.Season,
                        IsTorrent = h.IsTorrent,
                        Nodes = h.Node?.Flatten()
                            .Reverse()
                            .Select(node => new NodeBackupData
                            {
                                Id = node.Key,
                                Position = node.Position
                            })
                            .Where(node => node.Position > 0 || node.Id != h.ItemInfo.Site.Value)
                            .ToArray()
                    })
                    .ToArray();
            }

            if (items.Any())
            {
                backupData.Items = items
                    .GroupBy(i => (i.Site, i.SiteId))
                    .Select(g => g.First())
                    .Select(i => new ItemBackupData
                    {
                        Id = i.SiteId,
                        Site = i.Site,
                        Title = i.Title,
                        Link = i.Link == null ? null : new Uri(i.Link.GetPath(), UriKind.Relative),
                        AddDateTime = i.AddTime,
                        Poster = i.Poster.GetOrBigger(ImageSize.Preview),
                        Section = i.Section
                    })
                    .ToList();
            }

            return backupData;
        }

        /// <inheritdoc/>
        public async Task<BackupRestoreResult> RestoreFromBackupAsync(BackupData backupData, BackupDataTypes backupDataType, CancellationToken cancellationToken)
        {
            if (backupData.Version > Version)
            {
                throw new InvalidOperationException($"Attempt to restore backup from newest application version. Backup version: {backupData.Version}, max.supported version: {Version}.");
            }

            var result = new BackupRestoreResult();
            result.FavoritesCount = backupData.Favorites.Count;
            result.HistoryCount = backupData.History.Count;

            var settingsContainers = GetSettingsContainers(backupDataType).ToList();
            if (backupData.Settings != null
                && settingsContainers.Count > 0)
            {
                var backupedSettings = backupData.Settings
                    .SelectMany(p => p.Value.Select(v => (strategy: p.Key, container: v.Key, items: v.Value)))
                    .SelectMany(p => p.items.Select(s => (p.strategy, p.container, key: s.Key, value: s.Value)))
                    .Where(t => settingsContainers.Contains(t.container))
                    .ToArray();
                result.SettingsCount = backupedSettings.Length;

                foreach (var group in backupedSettings.GroupBy(t => (t.strategy, t.container)))
                {
                    result.SettingsRestoredCount += settingService.SetRawSettings(
                        group.Key.container,
                        group
                            .GroupBy(t => t.key)
                            .Select(g => (key: g.Key, value: g.Last().value switch
                            {
                                JsonElement { ValueKind: JsonValueKind.True } => (object?)true,
                                JsonElement { ValueKind: JsonValueKind.False } => false,
                                JsonElement { ValueKind: JsonValueKind.Number } number => number switch
                                {
                                    _ when number.TryGetInt32(out var int32) => int32,
                                    _ when number.TryGetInt64(out var int64) => int64,
                                    _ when number.TryGetDouble(out var @double) => @double,
                                    _ => throw new InvalidOperationException($"Not supported type {number}")
                                },
                                JsonElement { ValueKind: JsonValueKind.String } strNode => strNode.GetString(),
                                JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
                                string str => str,
                                bool boolean => boolean,
                                int int32 => int32,
                                long int64 => int64,
                                double @double => @double,
                                null => null,
                                { } unsupported => throw new InvalidOperationException($"Not supported type {unsupported}")
                            }))
                            .ToDictionary(t => t.key, t => t.value!),
                        group.Key.strategy);
                }
            }

            var items = backupData.Items.Select(item => new ItemInfo(item.Site, item.Id)
            {
                AddTime = item.AddDateTime ?? DateTimeOffset.Now,
                Title = item.Title,
                Link = item.Link,
                Poster = item.Poster,
                Section = item.Section
            });

            if (backupDataType.HasFlag(BackupDataTypes.Favorites))
            {
                var results = await backupData.Favorites
                    .Select(f => (
                        fav: f,
                        item: items.FirstOrDefault(i => f.Id == i.SiteId && f.Site == i.Site)))
                    .Where(t => t.item?.SiteId != null)
                    .Select(t => (
                        kind: Enum.TryParse<FavoriteListKind>(t.fav.KindNameId, out var kind) ? kind : FavoriteListKind.None,
                        t.item))
                    .Where(t => t.kind != FavoriteListKind.None
                        && favoriteManager.IsSupportedByProvider(t.item)
                        && favoriteManager.AvailableListKinds.Contains(t.kind))
                    .ToAsyncEnumerable()
                    .WhenAll((t, ct) => favoriteManager.AddToListAsync(t.item!, t.kind, ct).AsTask())
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                result.FavoritesRestoredCount += results.Count(r => r);
            }

            if (backupDataType.HasFlag(BackupDataTypes.History))
            {
                result.HistoryRestoredCount += await historyManager.UpsertAsync(backupData.History
                    .Select(h => (
                        hist: h,
                        item: items.FirstOrDefault(i => h.Id == i.SiteId && h.Site == i.Site)))
                    .Where(t => t.item?.SiteId != null)
                    .Select(t =>
                    {
                        var nodes = t.hist.Nodes?.AsEnumerable() ?? Enumerable.Empty<NodeBackupData>();
                        if (t.hist.Nodes?.LastOrDefault() is NodeBackupData rootNode
                            && rootNode.Id != t.item!.Site.Value)
                        {
                            nodes = new[]
                            {
                                new NodeBackupData
                                {
                                    Id = t.item.Site.Value
                                }
                            }.Concat(nodes);
                        }

                        var historyNode = nodes
                            .Where(node => node.Id != null)
                            .Select(node => new HistoryNode(node.Id!, node.Position))
                            .Aggregate((HistoryNode?)null, (parent, child) => { child.Parent = parent; return child; });
                        if (historyNode == null)
                        {
                            return null;
                        }

                        var historyItem = new HistoryItem(t.item!, historyNode)
                        {
                            Season = t.hist.Season,
                            Episode = t.hist.Episode,
                            AddTime = t.hist.AddDateTime,
                            IsTorrent = t.hist.IsTorrent
                        };

                        return historyItem;
                    })
                    .Where(item => item != null)!)
                    .ConfigureAwait(false);
            }

            return result;
        }

        /// <inheritdoc/>
        public BackupDataTypes GetPossibleTypes(BackupData backupData)
        {
            var possibleTypes = BackupDataTypes.None;

            if (backupData.Favorites?.Count > 0)
            {
                possibleTypes |= BackupDataTypes.Favorites;
            }

            if (backupData.History?.Count > 0)
            {
                possibleTypes |= BackupDataTypes.History;
            }

            if (backupData.Settings?.Count > 0)
            {
                if (backupData.Settings.Any(s => s.Value.ContainsKey(Settings.UserSettingsContainer)))
                {
                    possibleTypes |= BackupDataTypes.UserSettings;
                }

                if (backupData.Settings.Any(s => s.Value.ContainsKey(Settings.StateSettingsContainer)))
                {
                    possibleTypes |= BackupDataTypes.StateSettings;
                }

                if (backupData.Settings.Any(s => s.Value.ContainsKey(Settings.InternalSettingsContainer)))
                {
                    possibleTypes |= BackupDataTypes.InternalSettings;
                }
            }

            return possibleTypes;
        }

        private IEnumerable<string> GetSettingsContainers(BackupDataTypes backupDataType)
        {
            if (backupDataType.HasFlag(BackupDataTypes.UserSettings))
            {
                yield return Settings.UserSettingsContainer;
            }

            if (backupDataType.HasFlag(BackupDataTypes.StateSettings))
            {
                yield return Settings.StateSettingsContainer;
            }
        }
    }
}

namespace FSClient.Data.Repositories
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public sealed class HistoryJsonRepository : IHistoryRepository
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private const int MaxHistoryCount = 5000;
        private const string HistoryFileName = "history.json";

        internal readonly AsyncLazy<ConcurrentDictionary<string, (HistoryJsonEntity entity, HistoryItem item)>> historyAsyncLazy;

        private readonly SemaphoreSlim fileSemaphore;
        private readonly IStorageService storageService;
        private readonly ISettingService settingService;
        private readonly ILogger logger;

        public HistoryJsonRepository(
            IStorageService storageService,
            ISettingService settingService,
            ILogger logger)
        {
            this.storageService = storageService;
            this.settingService = settingService;
            this.logger = logger;

            fileSemaphore = new SemaphoreSlim(1);
            historyAsyncLazy = new AsyncLazy<ConcurrentDictionary<string, (HistoryJsonEntity entity, HistoryItem item)>>(LoadHistoryAsync);
        }

        public async ValueTask<int> DeleteManyAsync(IEnumerable<HistoryItem> items)
        {
            var history = await historyAsyncLazy.ConfigureAwait(false);
            var deletedCount = 0;
            var itemsArray = items.ToArray();

            foreach (var historyItem in itemsArray)
            {
                var itemRemoved = history!.TryRemove(historyItem.Key, out _);
                if (!itemRemoved
                    && historyItem.Node != null)
                {
                    var keysToRemoveByNode = history.Values.Select(t => t.item)
                        .Where(i => i.Node != null && FlattenWithoutSpecialNodes(i.Node).Any(n => n.Key == historyItem.Node.Key))
                        .Select(i => i.Key)
                        .Distinct()
                        .ToArray();

                    foreach (var item in keysToRemoveByNode)
                    {
                        itemRemoved |= history.TryRemove(item, out _);
                    }
                }
                if (itemRemoved)
                {
                    deletedCount++;
                }
            }
            SaveLegacyPosition(itemsArray);

            await SaveHistoryAsync().ConfigureAwait(false);

            return deletedCount;
        }

        public async ValueTask<bool> DeleteAsync(string id)
        {
            var item = await GetAsync(id).ConfigureAwait(false);
            if (item != null)
            {
                return await DeleteManyAsync(new[] { item }).ConfigureAwait(false) > 0;
            }
            return false;
        }

        public IAsyncEnumerable<HistoryItem> GetAll()
        {
            if (historyAsyncLazy.IsStarted
                && historyAsyncLazy.Task.Status == TaskStatus.RanToCompletion)
            {
                return historyAsyncLazy.Task.Result.Select(p => p.Value.item).ToAsyncEnumerable();
            }
            return EnumerableHelper
                .ToAsyncEnumerable(_ => historyAsyncLazy.Task)
                .SelectMany(history => history.Select(p => p.Value.item).ToAsyncEnumerable());
        }

        public IAsyncEnumerable<HistoryItem> GetOrderedHistory(string? itemInfoKey = null, int? season = null, Range? episode = null)
        {
            return GetAll()
                .Where(item => (itemInfoKey == null || item.ItemInfo.Key == itemInfoKey)
                    && (!season.HasValue || item.Season == season)
                    && (!episode.HasValue || (item.Episode is Range itemEpisode && itemEpisode.Equals(episode.Value))))
                .OrderByDescending(item => item.AddTime);
        }

        public async ValueTask<HistoryItem?> FindAsync(Expression<Func<HistoryItem, bool>> predicate)
        {
            var history = await historyAsyncLazy.ConfigureAwait(false);
            return history.Select(p => p.Value.item).FirstOrDefault(predicate.Compile());
        }

        public async ValueTask<HistoryItem?> GetAsync(string id)
        {
            var history = await historyAsyncLazy.ConfigureAwait(false);
            return history.TryGetValue(id, out var tuple) ? tuple.item : null;
        }

        public async ValueTask<int> UpsertManyAsync(IEnumerable<HistoryItem> items)
        {
            var history = await historyAsyncLazy.ConfigureAwait(false);
            var upsertedCount = 0;

            foreach (var item in items)
            {
                var newTupleValue = (JsonEntityFromItem(item), item);

                var result = history!.AddOrUpdate(item.Key,
                    (_) => newTupleValue,
                    (_, __) => newTupleValue);

                // There is no clean way to check if it was really updated or added.
                upsertedCount++;
            }
            SaveLegacyPosition(items);

            await SaveHistoryAsync().ConfigureAwait(false);

            return upsertedCount;
        }

        public async ValueTask<float> GetNodePositionById(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }
            var history = await historyAsyncLazy.ConfigureAwait(false);

            var position = history.FirstOrDefault(h => h.Value.item?.Node?.Key == id).Value.item?.Node?.Position
                ?? settingService.GetSetting(settingService.RootContainer, id, (float?)null, SettingStrategy.Local)
                ?? settingService.GetSetting(settingService.RootContainer, $"f{id}", (float?)null, SettingStrategy.Local)
                ?? settingService.GetSetting(settingService.RootContainer, id, (float?)null, SettingStrategy.Roaming)
                ?? settingService.GetSetting(settingService.RootContainer, $"f{id}", (float?)null, SettingStrategy.Roaming);

            return position ?? 0;
        }

        private static HistoryJsonEntity JsonEntityFromItem(HistoryItem item)
        {
            return new HistoryJsonEntity
            {
                AddTime = item.ItemInfo.AddTime ?? DateTimeOffset.Now,
                UpdateTime = item.AddTime,
                Episode = item.Episode,
                Season = item.Season,
                IsTorrent = item.IsTorrent,
                SiteId = item.ItemInfo.SiteId,
                Link = item.ItemInfo.Link,
                Poster = item.ItemInfo.Poster,
                Site = item.ItemInfo.Site,
                Title = item.ItemInfo.Title,
                Position = item.Node?.Position ?? 0,
                FileId = item.Node?.Key,
                Folders = item.Node?.Flatten().Except(new[] { item.Node }).Select(n => n.Key).Reverse()
            };
        }

        private async Task SaveHistoryAsync()
        {
            var history = await historyAsyncLazy.ConfigureAwait(false);
            if (storageService.LocalFolder is IStorageFolder folder)
            {
                try
                {
                    using var _ = await fileSemaphore.LockAsync(CancellationToken.None).ConfigureAwait(false);

                    var saveList = history.Values
                        .OrderByDescending(t => t.item.AddTime)
                        .Select(t => t.entity)
                        .Take(MaxHistoryCount)
                        .ToArray();

                    var file = await folder.CreateFileAsync(HistoryFileName, true).ConfigureAwait(false);
                    if (file == null)
                    {
                        logger.LogWarning("Can't save history: file is null.");
                        return;
                    }
                    await file.WriteJsonAsync(saveList, default).ConfigureAwait(false);
                }
                catch (System.IO.IOException ex)
                {
                    logger.LogWarning(ex);
                }
            }
            else
            {
                logger.LogWarning("Can't save history: either history collection or local folder is null.");
            }
        }

        private async Task<ConcurrentDictionary<string, (HistoryJsonEntity, HistoryItem)>> LoadHistoryAsync()
        {
            if (storageService.LocalFolder is not IStorageFolder folder)
            {
                return new ConcurrentDictionary<string, (HistoryJsonEntity, HistoryItem)>();
            }

            try
            {
                var file = await folder.GetFileAsync(HistoryFileName).ConfigureAwait(false);
                if (file != null)
                {
                    var items = (await file
                        .ReadFromJsonFileAsync<List<HistoryJsonEntity>>(default)
                        .ConfigureAwait(false))?
                        .Where(e => e?.SiteId != null && e.FileId != null)
                        .GroupBy(e => (e.Site, e.SiteId, e.Season, e.Episode))
                        .Select(e => e.First())
                        ?? Enumerable.Empty<HistoryJsonEntity>();

                    var allSettings = settingService
                        .GetAllRawSettings(settingService.RootContainer, SettingStrategy.Roaming)
                        .Concat(settingService
                        .GetAllRawSettings(settingService.RootContainer, SettingStrategy.Local))
                        .Where(v => v.Value is float)
                        .Select(v => (
                            key: v.Key,
                            value: v.Value as float?
                        ))
                        .Where(t => t.value.HasValue)
                        .GroupBy(t => t.key)
                        .ToDictionary(t => t.Key, t => t.Select(t => t.value).OfType<float>().Max());

                    return new ConcurrentDictionary<string, (HistoryJsonEntity, HistoryItem)>(items
                        .Select(entity =>
                        {
                            var node = (entity.Folders ?? Enumerable.Empty<string>())
                                .Union(new[] { entity.FileId })
                                .Where(id => !string.IsNullOrEmpty(id))
                                .Select(id =>
                                {
                                    if (!(allSettings.TryGetValue(id!, out var value) || allSettings.TryGetValue($"f{id}", out value)))
                                    {
                                        value = id == entity.FileId ? entity.Position : 0;
                                    }
                                    return new HistoryNode(id!, value);
                                })
                                .Aggregate((HistoryNode?)null, (parent, child) =>
                                {
                                    child.Parent = parent;
                                    return child;
                                });

                            var item = new ItemInfo(entity.Site, entity.SiteId)
                            {
                                Title = entity.Title,
                                AddTime = entity.AddTime,
                                Link = entity.Link,
                                Poster = entity.Poster,
                                Section = Section.CreateDefault(entity.Episode.HasValue
                                    ? SectionModifiers.Serial
                                    : SectionModifiers.Film)
                            };

                            return (entity, item: new HistoryItem(item, node!)
                            {
                                AddTime = entity.UpdateTime,
                                Season = entity.Season,
                                Episode = entity.Episode,
                                IsTorrent = entity.IsTorrent
                            });
                        })
                        .Where(t => t.item?.ItemInfo?.SiteId != null
                            && t.item.ItemInfo.Site != Site.Any
                            && t.item.Node != null)
                        .OrderByDescending(t => t.item.AddTime)
                        .ToDictionary(t => t.item.Key, t => t));
                }
                else
                {
                    return new ConcurrentDictionary<string, (HistoryJsonEntity, HistoryItem)>();
                }
            }
            catch (System.IO.IOException ex)
            {
                logger.LogCritical(ex);
                return new ConcurrentDictionary<string, (HistoryJsonEntity, HistoryItem)>();
            }
        }

        private void SaveLegacyPosition(IEnumerable<HistoryItem> historyItems)
        {
            var allSettings = (settingService
                .GetAllRawSettings(settingService.RootContainer, SettingStrategy.Local))
                .Concat(settingService
                .GetAllRawSettings(settingService.RootContainer, SettingStrategy.Roaming));

            foreach (var historyItem in historyItems)
            {
                if (historyItem.Node == null)
                {
                    continue;
                }

                var historyNodes = FlattenWithoutSpecialNodes(historyItem.Node);

                foreach (var node in historyNodes)
                {
                    if (node.Position < float.Epsilon)
                    {
                        var pairsToDelete = allSettings
                            .Where(pair => pair.Key.StartsWith(node.Key, StringComparison.OrdinalIgnoreCase)
                            || pair.Key.StartsWith($"f{node.Key}", StringComparison.OrdinalIgnoreCase));
                        foreach (var pairToDelete in pairsToDelete)
                        {
                            settingService.DeleteSetting(settingService.RootContainer, pairToDelete.Key, SettingStrategy.Local);
                            settingService.DeleteSetting(settingService.RootContainer, pairToDelete.Key, SettingStrategy.Roaming);
                        }
                    }
                    else
                    {
                        settingService.SetSetting(settingService.RootContainer, node.Key, node.Position, SettingStrategy.Local);
                    }
                }
            }
        }

        private static IEnumerable<HistoryNode> FlattenWithoutSpecialNodes(HistoryNode input)
        {
            return input.Flatten().Where(node =>
                node.Key != Site.Any.Value
                && node.Key != Site.All.Value
                && !Site.TryParse(node.Key, out _, allowUnknown: false));
        }
    }
}

namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

    /// <inheritdoc/>
    public sealed class FolderManager : IFolderManager
    {
        private readonly Dictionary<Site, (IFileProvider fileProvider, ISearchProvider? searchProvider)> providers;

        private readonly IHistoryManager historyManager;
        private readonly IItemManager itemManager;
        private readonly IUserManager userManager;
        private readonly IProviderManager providerManager;
        private readonly ITorrServerService torrServerService;
        private readonly ILogger logger;

        public FolderManager(
            IEnumerable<IFileProvider> fileProviders,
            IEnumerable<ISearchProvider> searchProviders,
            IHistoryManager historyManager,
            IItemManager itemManager,
            IUserManager userManager,
            IProviderManager providerManager,
            ITorrServerService torrServerService,
            ILogger logger)
        {
            this.historyManager = historyManager;
            this.itemManager = itemManager;
            this.userManager = userManager;
            this.providerManager = providerManager;
            this.torrServerService = torrServerService;
            this.logger = logger;

            providers = fileProviders.ToDictionary(p => p.Site, p => (p, searchProviders.FirstOrDefault(sp => sp.Site == p.Site)))!;
        }

        /// <inheritdoc/>
        public async Task<(IFolderTreeNode? folder, HistoryItem? historyItem)> GetFolderFromHistoryAsync(
            ItemInfo item, HistoryItem? historyItem, CancellationToken cancellationToken)
        {
            try
            {
                var isNotBlocked = await itemManager.IsNotItemBlockedAsync(item, false, cancellationToken).ConfigureAwait(false);
                if (!isNotBlocked)
                {
                    return (GetBlockedFolder(), historyItem);
                }

                historyItem ??= (await historyManager
                    .GetHistory()
                    .Where(i => i.ItemInfo == item)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false));
                if (historyItem == null)
                {
                    return default;
                }

                item ??= historyItem.ItemInfo;
                if (item == null)
                {
                    return default;
                }

                var ids = historyItem.Node?.Flatten().Select(n => n.Key) ?? Enumerable.Empty<string>();

                if (Settings.Instance.LoadAllSources)
                {
                    ids = ids.Concat(new[] { Site.All.Value });
                }

                var stackIds = new Stack<string>(ids);

                var folder = await LoadFolderFromIDsPathAsync(item, stackIds, historyItem.IsTorrent, cancellationToken).ConfigureAwait(false);
                var lastFile = folder?.ItemsSource.OfType<File>().FirstOrDefault(f => f.Id == historyItem.Node?.Key);

                return (folder, historyItem);
            }
            catch (OperationCanceledException)
            {
                return default;
            }
            catch (Exception ex)
            {
                ex.Data["Item"] = item;
                logger?.LogError(ex);
                return default;
            }
        }

        /// <inheritdoc/>
        public Task<(Folder? Folder, ProviderResult Result)> GetFilesRootAsync(
            ItemInfo item, Site site, CancellationToken cancellationToken)
        {
            return GetRootAsync(item, site, false, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<(Folder? Folder, ProviderResult Result)> GetTorrentsRootAsync(
            ItemInfo item, Site site, CancellationToken cancellationToken)
        {
            return GetRootAsync(item, site, true, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<ProviderResult> OpenFolderAsync(
            IFolderTreeNode folder, CancellationToken cancellationToken)
        {
            try
            {
                if (folder == null)
                {
                    throw new ArgumentNullException(nameof(folder));
                }

                var result = ProviderResult.Success;

                if (folder.Count == 0)
                {
                    if (folder is TorrentFolder torrentFolder
                        && Settings.Instance.TorrServerEnabled)
                    {
                        var isAvailable = await torrServerService.IsTorrServerAvailableAsync(cancellationToken)
                            .ConfigureAwait(false);
                        if (!isAvailable)
                        {
                            result = ProviderResult.NotAvailable;
                        }
                        else
                        {
                            var preloaded = await torrentFolder.PreloadAsync(cancellationToken).ConfigureAwait(false);
                            if (!preloaded)
                            {
                                return ProviderResult.Failed;
                            }

                            var hashId = await torrServerService.AddOrUpdateTorrentAsync(torrentFolder, cancellationToken)
                                .ConfigureAwait(false);
                            var nodes = await torrServerService.GetTorrentNodesAsync(torrentFolder, hashId, cancellationToken).ConfigureAwait(false);
                            torrentFolder.AddRange(nodes);
                        }
                    }
                    else
                    {
                        if (!providers.TryGetValue(folder.Site, out var provider))
                        {
                            return ProviderResult.NotSupported;
                        }

                        var isAllowed = await userManager.CheckRequirementsAsync(provider.fileProvider.Site, provider.fileProvider.ReadRequirements, cancellationToken).ConfigureAwait(false);
                        if (!isAllowed)
                        {
                            result = ProviderResult.NeedLogin;
                        }
                        else
                        {
                            var children = await provider.fileProvider.GetFolderChildrenAsync((Folder)folder, cancellationToken).ConfigureAwait(false);
                            folder.AddRange(children);

                            result = folder.Count > 0 ? ProviderResult.Success
                                : cancellationToken.IsCancellationRequested ? ProviderResult.Canceled
                                : ProviderResult.Failed;
                        }
                    }
                }

                await Task.WhenAll(folder.ItemsSource
                    .Select(historyManager.LoadPositionToNodeAsync))
                    .ConfigureAwait(false);

                return result;
            }
            catch (UnauthorizedAccessException)
            {
                return ProviderResult.NeedLogin;
            }
            catch (HttpRequestException)
            {
                return ProviderResult.Failed;
            }
            catch (OperationCanceledException)
            {
                return ProviderResult.Canceled;
            }
            catch (Exception ex)
            {
                ex.Data["Item"] = folder?.ItemInfo;
                ex.Data["Folder"] = folder == null ? "null" : $"{folder.Site} {folder.Title}: {folder.Id}";

                logger?.LogError(ex);

                return ProviderResult.Failed;
            }
        }

        /// <inheritdoc/>
        public async Task<IFolderTreeNode?> ReloadFolderAsync(
            IFolderTreeNode folder, CancellationToken cancellationToken)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }
            if (folder.ItemInfo is not ItemInfo item)
            {
                throw new InvalidOperationException("Folder ItemInfo must be not-null");
            }

            var tempFolder = folder;
            try
            {
                if (folder.Site == Site.All)
                {
                    (tempFolder, _) = folder.IsTorrent
                        ? await GetTorrentsRootAsync(item, Site.All, cancellationToken).ConfigureAwait(false)
                        : await GetFilesRootAsync(item, Site.All, cancellationToken).ConfigureAwait(false);
                }
                else if (folder.Parent == null)
                {
                    (tempFolder, _) = folder.IsTorrent
                        ? await GetTorrentsRootAsync(item, folder.Site, cancellationToken).ConfigureAwait(false)
                        : await GetFilesRootAsync(item, folder.Site, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var ids = folder.GetIDsStack();

                    tempFolder = await LoadFolderFromIDsPathAsync(item, ids, folder.IsTorrent, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                ex.Data["Item"] = folder.ItemInfo;
                ex.Data["Folder"] = $"{folder.Site} {folder.Title}: {folder.Id}";
                logger?.LogError(ex);
            }
            return tempFolder;
        }

        private async Task<(Folder Folder, ProviderResult Result)> GetRootByProviderAsync(
            ItemInfo item, IFileProvider provider, ISearchProvider? searchProvider, bool getTorrents, CancellationToken firstStepCancellationToken, CancellationToken cancellationToken)
        {
            var isNotBlocked = await itemManager.IsNotItemBlockedAsync(item, false, cancellationToken).ConfigureAwait(false);
            if (!isNotBlocked)
            {
                return (GetBlockedFolder(), ProviderResult.Blocked);
            }

            var folder = new Folder(provider.Site, "", FolderType.ProviderRoot, PositionBehavior.None)
            {
                ItemInfo = item,
                IsTorrent = getTorrents
            };

            if (item == null)
            {
                return (folder, ProviderResult.Failed);
            }

            ProviderResult inited = ProviderResult.Unknown, loaded = ProviderResult.Unknown;
            try
            {
                await Task
                    .Run(GetRoot, cancellationToken)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (inited != ProviderResult.Success && firstStepCancellationToken.IsCancellationRequested)
                {
                    inited = ProviderResult.Canceled;
                }
                if (loaded != ProviderResult.Success && cancellationToken.IsCancellationRequested)
                {
                    loaded = ProviderResult.Canceled;
                }

                return (folder, loaded == ProviderResult.Unknown ? inited : loaded);
            }
            catch (OperationCanceledException)
            {
                return (folder, ProviderResult.Canceled);
            }
            catch (Exception ex)
            {
                ex.Data["Item"] = item;
                ex.Data["Provider"] = provider + " " + provider.Site;
                ex.Data["Inited"] = inited.ToString();
                ex.Data["Loaded"] = loaded.ToString();
                logger?.LogError(ex);
                return (folder, ProviderResult.Failed);
            }

            async Task GetRoot()
            {
                var isAllowed = await userManager.CheckRequirementsAsync(provider.Site, provider.ReadRequirements, firstStepCancellationToken).ConfigureAwait(false);
                if (!isAllowed)
                {
                    var user = await userManager.GetCurrentUserAsync(provider.Site, firstStepCancellationToken).ConfigureAwait(false);
                    if (provider.ReadRequirements.HasFlag(ProviderRequirements.ProForAny) && user?.HasProStatus == false)
                    {
                        inited = ProviderResult.NeedProAccount;
                    }
                    else
                    {
                        inited = ProviderResult.NeedLogin;
                    }
                    return;
                }

                var items = new[] { item };

                if (searchProvider != null)
                {
                    isAllowed = await userManager.CheckRequirementsAsync(searchProvider.Site, searchProvider.ReadRequirements, firstStepCancellationToken).ConfigureAwait(false);
                    if (!isAllowed)
                    {
                        return;
                    }

                    items = (await searchProvider.FindSimilarAsync(item, firstStepCancellationToken).ConfigureAwait(false)).ToArray();
                    if (items.Length == 0)
                    {
                        inited = ProviderResult.NotFound;
                        return;
                    }
                }

                inited = ProviderResult.Success;

                if (getTorrents)
                {
                    var folderPerItem = await items
                        .ToAsyncEnumerable()
                        .WhenAll((i, ct) => provider.GetTorrentsRootAsync(i, ct))
                        .Where(folder => folder?.Count > 0)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (folderPerItem.Count == 1)
                    {
                        folder.AddRange(folderPerItem[0]!);
                    }
                    else
                    {
                        folder.AddRange(folderPerItem!);
                    }

                    loaded = folder.Count > 0 ? ProviderResult.Success
                        : cancellationToken.IsCancellationRequested ? ProviderResult.Canceled
                        : ProviderResult.NotFound;
                }
                else
                {
                    provider.InitForItems(items);
                    var children = await provider.GetFolderChildrenAsync(folder, cancellationToken).ConfigureAwait(false);
                    folder.AddRange(children);

                    loaded = folder.Count > 0 ? ProviderResult.Success
                        : cancellationToken.IsCancellationRequested ? ProviderResult.Canceled
                        : ProviderResult.Failed;
                }
            }
        }

        private async Task<(Folder? Folder, ProviderResult Result)> GetRootAsync(
            ItemInfo item, Site site, bool getTorrents, CancellationToken cancellationToken)
        {
            if (item == null)
            {
                return (null, ProviderResult.Failed);
            }

            var returnTuple = (folder: (Folder?)null, result: ProviderResult.NotFound);

            var availableProviders = providerManager.GetOrderedProviders()
                .Select(site => (
                    valid: providers.TryGetValue(site, out var provider),
                    provider
                ))
                .Where(t => t.valid && (getTorrents ? t.provider.fileProvider!.ProvideTorrent : t.provider.fileProvider!.ProvideOnline)
                    && providerManager.IsProviderEnabled(t.provider.fileProvider.Site))
                .Select(t => t.provider);

            if (!availableProviders.Any())
            {
                return (null, ProviderResult.NoValidProvider);
            }

            if (Settings.Instance.PreferItemSite)
            {
                availableProviders = availableProviders.OrderByDescending(p => p.fileProvider.Site == item.Site);
            }

            if (site == Site.Any)
            {
                foreach (var (fileProvider, searchProvider) in availableProviders)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return (null, ProviderResult.Canceled);
                    }

                    using (var initStepTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    using (var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    using (var initStepLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(initStepTokenSource.Token, cancellationToken))
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token, cancellationToken))
                    {
                        var (folder, result) = await GetRootByProviderAsync(item, fileProvider, searchProvider, getTorrents, initStepLinkedCts.Token, linkedCts.Token).ConfigureAwait(false);

                        if (result == ProviderResult.Success)
                        {
                            returnTuple = (folder, result);
                            break;
                        }
                    }
                    if (returnTuple.folder == null
                        && returnTuple.result == ProviderResult.NotFound
                        && cancellationToken.IsCancellationRequested)
                    {
                        returnTuple = (null, ProviderResult.Canceled);
                    }
                }
            }
            else if (site == Site.All)
            {
                var folder = new Folder(Site.All, "", FolderType.ProviderRoot, PositionBehavior.None)
                {
                    ItemInfo = item,
                    IsTorrent = getTorrents
                };
                ProviderResult result;

                using (var initStepTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                using (var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                using (var initStepLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(initStepTokenSource.Token, cancellationToken))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token, cancellationToken))
                {
                    var results = await Task
                        .WhenAll(availableProviders
                        .Select(provider => GetRootByProviderAsync(item, provider.fileProvider, provider.searchProvider, getTorrents, initStepLinkedCts.Token, linkedCts.Token)))
                        .ConfigureAwait(false);

                    var nodes = results
                        .Where(r => r.Result == ProviderResult.Success && r.Folder != null)
                        .Select(r => r.Folder)
                        .SelectMany(providerFolder =>
                        {
                            foreach (var node in providerFolder.ItemsSource)
                            {
                                node.Group = providerFolder.Site.Title;
                            }

                            return providerFolder.ItemsSource;
                        })
                        .ToList();

                    folder.AddRange(nodes);

                    result = nodes.Count > 0 ? ProviderResult.Success
                        : linkedCts.IsCancellationRequested ? ProviderResult.Canceled
                        : ProviderResult.Failed;
                }
                returnTuple = (folder, result);
            }
            else if (providers.TryGetValue(site, out var provider)
                    && ((provider.fileProvider.ProvideOnline && !getTorrents)
                        || (provider.fileProvider.ProvideTorrent && getTorrents)))
            {
                returnTuple = await GetRootByProviderAsync(item, provider.fileProvider, provider.searchProvider, getTorrents, cancellationToken, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return (null, ProviderResult.NoValidProvider);
            }

            if (returnTuple.folder != null)
            {
                await Task.WhenAll(returnTuple.folder.ItemsSource
                        .Select(historyManager.LoadPositionToNodeAsync))
                    .ConfigureAwait(false);
            }

            return returnTuple;
        }

        private async Task<IFolderTreeNode?> LoadFolderFromIDsPathAsync(
            ItemInfo itemInfo, Stack<string> idsStack, bool torrents, CancellationToken cancellationToken)
        {
            var rootSite = idsStack.Select(id => Site.Parse(id, Site.Any)).LastOrDefault(site => site != Site.Any && site != Site.All);
            idsStack = new Stack<string>(idsStack
                .SkipWhile(id => Site.TryParse(id, out _))
                .Reverse());

            var (rootFolderTemp, result) = torrents
                ? await GetTorrentsRootAsync(itemInfo, rootSite, cancellationToken).ConfigureAwait(false)
                : await GetFilesRootAsync(itemInfo, rootSite, cancellationToken).ConfigureAwait(false);
            var rootFolder = (IFolderTreeNode?)rootFolderTemp;
            if (rootFolder == null
                || result != ProviderResult.Success)
            {
                return null;
            }

            var tempFolder = rootFolder;

            while (idsStack.Count > 0
                && tempFolder.Count > 0)
            {
                var nextId = idsStack.Pop();
                var parent = tempFolder;

                tempFolder = parent.ItemsSource
                    .OfType<IFolderTreeNode>()
                    .FirstOrDefault(f => f.Id == nextId);

                if (tempFolder == null)
                {
                    tempFolder = parent.ItemsSource
                        .OfType<IFolderTreeNode>()
                        .FirstOrDefault(f => nextId.StartsWith(f.Id, StringComparison.Ordinal));
                    if (tempFolder != null)
                    {
                        idsStack.Push(nextId);
                    }
                }

                if (tempFolder == null)
                {
                    tempFolder = parent;
                    continue;
                }
                await OpenFolderAsync(tempFolder, cancellationToken).ConfigureAwait(false);
            }
            return tempFolder;
        }

        private static Folder GetBlockedFolder()
        {
            return new Folder(Site.Any, string.Empty, FolderType.Unknown, PositionBehavior.None)
            {
                PlaceholderText = Strings.Folders_ItemIsBlocked
            };
        }
    }
}

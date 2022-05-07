namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    /// <inheritdoc/>
    public sealed class FileManager : IFileManager
    {
        private readonly Dictionary<Site, (IFileProvider fileProvider, ISearchProvider? searchProvider)> providers;

        private readonly IHistoryManager historyManager;
        private readonly IUserManager userManager;
        private readonly IProviderManager providerManager;
        private readonly ILauncherService launcherService;
        private readonly IShareService shareService;
        private readonly ITorrServerService torrServerService;
        private readonly IThirdPartyPlayer thirdPartyPlayer;
        private readonly ILogger logger;

        public FileManager(
            IEnumerable<IFileProvider> fileProviders,
            IEnumerable<ISearchProvider> searchProviders,
            IHistoryManager historyManager,
            IUserManager userManager,
            IProviderManager providerManager,
            ILauncherService launcherService,
            IShareService shareService,
            ITorrServerService torrServerService,
            IThirdPartyPlayer thirdPartyPlayer,
            ILogger logger)
        {
            this.historyManager = historyManager;
            this.userManager = userManager;
            this.providerManager = providerManager;
            this.launcherService = launcherService;
            this.shareService = shareService;
            this.torrServerService = torrServerService;
            this.thirdPartyPlayer = thirdPartyPlayer;
            this.logger = logger;

            providers = fileProviders.ToDictionary(p => p.Site, p => (p, searchProviders.FirstOrDefault(sp => sp.Site == p.Site)))!;
        }

        /// <inheritdoc/>
        public event Action<Video, NodeOpenWay>? VideoOpened;

        /// <inheritdoc/>
        public Video? LastVideo { get; private set; }

        /// <inheritdoc/>
        public async Task<IEnumerable<ITreeNode>> GetTrailersAsync(
            ItemInfo item, CancellationToken cancellationToken)
        {
            if (item == null)
            {
                return Enumerable.Empty<File>();
            }

            var availableProviders = providerManager.GetOrderedProviders()
                .Select(s => (
                    valid: providers.TryGetValue(s, out var providerTuple),
                    providerTuple
                ))
                .Where(t => t.valid && t.providerTuple.fileProvider!.ProvideTrailers && providerManager.IsProviderEnabled(t.providerTuple.fileProvider.Site))
                .Select(t => t.providerTuple);
            if (Settings.Instance.PreferItemSite)
            {
                availableProviders = availableProviders.OrderByDescending(p => p.fileProvider.Site == item.Site);
            }

            foreach (var provider in availableProviders)
            {
                try
                {
                    var isAllowed = await userManager.CheckRequirementsAsync(provider.fileProvider.Site, provider.fileProvider.ReadRequirements, cancellationToken).ConfigureAwait(false);
                    if (!isAllowed)
                    {
                        continue;
                    }

                    var items = new[] { item }.AsEnumerable();

                    if (provider.searchProvider != null)
                    {
                        isAllowed = await userManager.CheckRequirementsAsync(provider.searchProvider.Site, provider.searchProvider.ReadRequirements, cancellationToken).ConfigureAwait(false);
                        if (!isAllowed)
                        {
                            continue;
                        }

                        items = await provider.searchProvider.FindSimilarAsync(item, cancellationToken).ConfigureAwait(false);
                    }

                    var folders = await items
                        .ToAsyncEnumerable()
                        .WhenAll((i, ct) => provider.fileProvider.GetTrailersRootAsync(i, ct))
                        .Where(folder => folder?.Count > 0)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    var files = folders
                        .SelectMany(f => f!.ItemsSource)
                        // We don't support complex nodes tree for trailers yet
                        .OfType<File>()
                        .ToList();
                    if (files?.Count > 0)
                    {
                        files.PrepareNodes(item);
                        return files;
                    }
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    ex.Data["Item"] = item;
                    ex.Data["Provider"] = provider + " " + provider.fileProvider.Site;
                    logger?.LogError(ex);
                }
            }

            return Enumerable.Empty<File>();
        }

        /// <inheritdoc/>
        public bool IsOpenWayAvailableForNode(
            ITreeNode node, NodeOpenWay way)
        {
            return node switch
            {
                TorrentFolder _ => way == NodeOpenWay.InBrowser
                    || way == NodeOpenWay.Remote
                    || way == NodeOpenWay.In3rdPartyApp
                    || way == NodeOpenWay.CopyLink
                    || (way == NodeOpenWay.InApp && Settings.Instance.TorrServerEnabled),
                Folder _ => way == NodeOpenWay.InApp,
                File _ => true,
                _ => false,
            };
        }

        /// <inheritdoc/>
        public async Task<bool> OpenFileAsync(
            File file, NodeOpenWay way, CancellationToken cancellationToken)
        {
            try
            {
                if (file == null)
                {
                    throw new ArgumentNullException(nameof(file));
                }

                var result = await file.PreloadAsync(cancellationToken).ConfigureAwait(false);
                if (!result
                    || cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                var video = await file.GetDefaultAsync(cancellationToken).ConfigureAwait(false);
                if (video == null
                    || cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                return await OpenVideoAsync(video, way, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ex.Data["Item"] = file?.ItemInfo;
                ex.Data["Provider"] = file?.ItemInfo?.Site;
                ex.Data["File"] = file;
                ex.Data["FileOpenWay"] = way;
                logger?.LogError(ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> OpenVideoAsync(
            Video video, NodeOpenWay way, CancellationToken cancellationToken)
        {
            try
            {
                if (video == null)
                {
                    throw new ArgumentNullException(nameof(video));
                }

                var parentFile = video.ParentFile;

                var result = false;

                switch (way)
                {
                    case NodeOpenWay.InApp:
                    case NodeOpenWay.InSeparatedWindow:
                        result = true;
                        break;
                    case NodeOpenWay.InBrowser
                    when Settings.Instance.OpenDirectLinkInBrowser
                        && video.SingleLink is Uri singleLink:
                        result = await launcherService.LaunchUriAsync(singleLink).ConfigureAwait(false) == LaunchResult.Success;
                        break;
                    case NodeOpenWay.InBrowser
                    when (video.ParentFile?.FrameLink ?? video.SingleLink) is Uri videoLink:
                        result = await launcherService.LaunchUriAsync(videoLink).ConfigureAwait(false) == LaunchResult.Success;
                        break;
                    case NodeOpenWay.Remote:
                        Uri? fsclientProtocolUri = null;
                        if (parentFile != null
                            && UriParserHelper.GenerateUriFromNode(parentFile) is Uri remoteUri)
                        {
                            fsclientProtocolUri = remoteUri;
                        }

                        var remoteResult = await launcherService.RemoteLaunchUriAsync(new RemoteLaunchDialogInput(fsclientProtocolUri, video.SingleLink, true, null)).ConfigureAwait(false);
                        result = remoteResult.IsSuccess;
                        break;
                    case NodeOpenWay.CopyLink:
                        var copyLinksStr = string.Join(Environment.NewLine, video.Links.Select(l => l.ToString()));
                        result = await shareService.CopyTextToClipboardAsync(copyLinksStr).ConfigureAwait(false);
                        break;
                    case NodeOpenWay.In3rdPartyApp:
                        var thirdPartyResult = await thirdPartyPlayer.OpenVideoAsync(video, video.ParentFile?.SubtitleTracks.FirstOrDefault(), cancellationToken).ConfigureAwait(false);
                        result = thirdPartyResult == ThirdPartyPlayerOpenResult.Success;
                        break;
                    case NodeOpenWay.InBrowser:
                        break;
                }

                if (result)
                {
                    if (parentFile != null
                        && way != NodeOpenWay.InApp
                        && way != NodeOpenWay.InSeparatedWindow)
                    {
                        parentFile.IsWatched = true;
                    }

                    await OnVideoOpenedAsync(video, way).ConfigureAwait(false);
                }

                return result;
            }
            catch (Exception ex)
            {
                ex.Data["Item"] = video?.ParentFile?.ItemInfo;
                ex.Data["Provider"] = video?.ParentFile?.ItemInfo?.Site;
                ex.Data["Video"] = video;
                ex.Data["FileOpenWay"] = way;
                logger?.LogError(ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> PreloadNodesAsync(
            IEnumerable<IPreloadableNode> nodes, bool preloadEpisodes, HistoryItem? historyItem, CancellationToken cancellationToken)
        {
            try
            {
                // Avoid nodes preloading inside of torrent folders (like nodes from TorrServer)
                nodes = nodes.Where(node => node is ITorrentTreeNode || !node.IsTorrent);
                var nodesArray = nodes.ToArray();

                if (nodesArray.Length == 0)
                {
                    return false;
                }

                if (nodesArray.All(f => !f.Episode.HasValue))
                {
                    return await nodesArray.ToAsyncEnumerable()
                        .WhenAll(PreloadSingleNodeAsync)
                        .AllAsync(r => r, cancellationToken)
                        .ConfigureAwait(false);
                }

                var parent = nodesArray.Select(f => f.Parent).FirstOrDefault(f => f != null);
                if (!preloadEpisodes
                    || parent == null)
                {
                    return false;
                }

                var startIndex = 0;

                // It preloads from last viewed node
                var last = await historyManager.GetLastViewedFolderChildAsync<IPreloadableNode>(parent, historyItem).ConfigureAwait(false);

                if (last != null
                    && Array.IndexOf(nodesArray, last) is var index
                    && index > 0)
                {
                    startIndex = index;
                }

                var beforeTake = Math.Min(startIndex, 10);
                const int afterTake = 20;

                return await nodesArray
                    .Skip(startIndex)
                    .Take(afterTake + (10 - beforeTake))
                    .Concat(nodesArray.Skip(startIndex - beforeTake).Take(beforeTake))
                    .ToAsyncEnumerable()
                    .WhenAll(PreloadSingleNodeAsync)
                    .AllAsync(r => r, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            async Task<bool> PreloadSingleNodeAsync(IPreloadableNode node, CancellationToken cancellationToken)
            {
                try
                {
                    var result = await node.PreloadAsync(cancellationToken).ConfigureAwait(false);
                    if (node is File file)
                    {
                        file.UpdateQuality();
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex);
                    return false;
                }
            }
        }

        /// <inheritdoc/>
        public Task HandleVideoStopedAsync(Video video, CancellationToken cancellationToken)
        {
            if (video.ParentFile == null
                || !video.ParentFile.IsTorrent)
            {
                return Task.CompletedTask;
            }

            var rootTorrentNode = video.ParentFile
                .ParentsEnumerable<ITorrentTreeNode>()
                .FirstOrDefault();
            var torrentHash = rootTorrentNode?
                .TorrentHash;
            if (torrentHash == null)
            {
                return Task.CompletedTask;
            }

            if (rootTorrentNode is TorrentFolder torrentFolder)
            {
                foreach (var file in torrentFolder.GetDeepNodes<File>())
                {
                    file.SetVideos(Array.Empty<Video>());
                }
            }
            
            return torrServerService.StopTorrentAsync(torrentHash, cancellationToken);
        }

        private async Task OnVideoOpenedAsync(
            Video video, NodeOpenWay openWay)
        {
            try
            {
                LastVideo = video;

                if (video?.ParentFile is File file)
                {
                    VideoOpened?.Invoke(video, openWay);

                    if (file.Position > float.Epsilon)
                    {
                        await historyManager.UpsertAsync(new[] { file }).ConfigureAwait(false);
                    }

                    var logProps = file.GetLogProperties(false);
                    logProps["OpenWay"] = openWay.ToString();
                    logger?.Log(LogLevel.Information, default, logProps, null, (_, __) => "VideoOpening");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }
        }
    }
}

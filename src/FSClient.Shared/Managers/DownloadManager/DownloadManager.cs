namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using PlaylistsNET.Content;
    using PlaylistsNET.Models;

    using File = FSClient.Shared.Models.File;

    /// <inheritdoc cref="IDownloadManager" />
    public sealed class DownloadManager : IDownloadManager, IDisposable
    {
        private const string VideoDownloadFolderKey = "downloadFolder";
        private const string TorrentFilesDownloadFolderKey = "torrentFolder";

        private IStorageFolder? cachedFolder;
        private IStorageFolder? cachedTorrentFolder;

        private readonly HttpClient httpClient;
        private readonly ILogger logger;
        private readonly IDownloadRepository downloadRepository;
        private readonly ISettingService settingService;
        private readonly IStorageService storageService;
        private readonly IDownloadService downloadService;
        private readonly IShareService shareService;
        private readonly ILauncherService launcherService;

        public DownloadManager(
            IDownloadRepository downloadRepository,
            IStorageService storageService,
            ISettingService settingService,
            IDownloadService downloadService,
            IShareService shareService,
            ILauncherService launcherService,
            ILogger logger)
        {
            this.logger = logger;
            this.downloadRepository = downloadRepository;
            this.storageService = storageService;
            this.settingService = settingService;
            this.downloadService = downloadService;
            this.shareService = shareService;
            this.launcherService = launcherService;

            this.downloadService.DownloadProgressChanged += DownloadService_DownloadProgressChanged;

            httpClient = new HttpClient(new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            });
        }

        /// <inheritdoc/>
        public event EventHandler<EventArgs<IEnumerable<DownloadFile>>>? FilesRemoved;

        /// <inheritdoc/>
        public event EventHandler<DownloadEventArgs>? DownloadProgressChanged;

        /// <inheritdoc/>
        public async Task<DownloadFile?> GetDownloadFileByVideo(Video video)
        {
            if (video.ParentFile?.Id is not string fileId)
            {
                return null;
            }

            return await downloadRepository
                .GetAllByFileId(fileId)
                .Where(entity => entity.FilePath != null)
                .OrderByDescending(entity => entity.VideoQuality)
                .ToAsyncEnumerable()
                .SelectAwait(async entity =>
                {
                    var file = await storageService.OpenFileFromPathAsync(entity.FilePath!).ConfigureAwait(false);
                    if (file == null)
                    {
                        return null;
                    }

                    var size = file.SizeInBytes;
                    var downloadedFile = new DownloadFile(entity.OperationId, file, entity.AddTime);
                    downloadedFile.Status = size > 0 ? DownloadStatus.Completed
                        : DownloadStatus.Error;
                    downloadedFile.BytesReceived = size;
                    downloadedFile.TotalBytesToReceive = entity.TotalBytesToReceive;
                    return downloadedFile;
                })
                .FirstOrDefaultAsync(file => file != null)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<DownloadFile>> GetDownloadsAsync(
            CancellationToken cancellationToken)
        {
            var activeDownloadsTask = downloadService.GetActiveDownloadsAsync();

            try
            {
                var allDownloadsTask = downloadRepository.GetAll().ToListAsync(cancellationToken).AsTask();
                await Task.WhenAll(allDownloadsTask, activeDownloadsTask).ConfigureAwait(false);
                var allDownloads = allDownloadsTask.Result;
                var activeDownloads = activeDownloadsTask.Result;

                var downloads = await allDownloads
                    .ToAsyncEnumerable()
                    .SelectAwait(async entity =>
                    {
                        if (entity.FilePath == null)
                        {
                            return (entity, download: (DownloadFile?)null);
                        }

                        var activeDownload = activeDownloads
                            .FirstOrDefault(d => d.OperationId == entity.OperationId);
                        if (activeDownload != null)
                        {
                            return (entity, download: activeDownload);
                        }

                        var file = await storageService.OpenFileFromPathAsync(entity.FilePath!).ConfigureAwait(false);
                        var size = file?.SizeInBytes ?? 0;

                        var nonActiveDownload = file != null
                            ? new DownloadFile(entity.OperationId, file, entity.AddTime)
                            : new DownloadFile(entity.OperationId, Path.GetFileNameWithoutExtension(entity.FilePath), entity.AddTime);

                        nonActiveDownload.Status = file == null ? DownloadStatus.FileMissed
                            : size > 0 ? DownloadStatus.Completed
                            : DownloadStatus.Error;
                        nonActiveDownload.BytesReceived = size;
                        nonActiveDownload.TotalBytesToReceive = entity.TotalBytesToReceive;

                        return (entity, download: nonActiveDownload);
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (downloads.Any(t => t.download == null))
                {
                    await downloadRepository.DeleteManyAsync(downloads
                        .Where(t => t.download == null).Select(t => t.entity))
                        .ConfigureAwait(false);
                    downloads = downloads.Where(t => t.download != null).ToList();
                }

                var missedDownloads = activeDownloads
                    .Where(download => allDownloads.All(e => e.OperationId != download.OperationId) && download.File != null)
                    .Select(download => (
                        download,
                        entity: new DownloadEntity(
                            download.OperationId,
                            null,
                            null,
                            download.File!.Path,
                            download.TotalBytesToReceive,
                            download.AddTime)))
                    .ToArray();

                if (missedDownloads.Length > 0)
                {
                    downloads.AddRange(missedDownloads.Select(t => (t.entity, t.download))!);
                    await downloadRepository
                        .UpsertManyAsync(missedDownloads.Select(t => t.entity))
                        .ConfigureAwait(false);
                }

                return downloads
                    .Select(t => t.download)!;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
                return (await activeDownloadsTask.ConfigureAwait(false));
            }
        }

        /// <inheritdoc/>
        public async Task RemoveFilesAsync(IEnumerable<DownloadFile> files, bool deleteFromDevice)
        {
            var filesToRemove = files.ToArray();
            await filesToRemove.ToAsyncEnumerable()
                .WhenAllAsync((f, _) => downloadService.CancelDownloadAsync(f))
                .ConfigureAwait(false);

            try
            {
                await downloadRepository
                    .DeleteManyAsync(filesToRemove.Select(f => new DownloadEntity() { OperationId = f.OperationId }))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }

            FilesRemoved?.Invoke(this, new EventArgs<IEnumerable<DownloadFile>>(filesToRemove));

            if (deleteFromDevice)
            {
                await filesToRemove.Where(f => f.File != null)
                    .ToAsyncEnumerable()
                    .WhenAllAsync((f, _) => f.File!.DeleteAsync())
                    .ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task TogglePlayPauseAsync(DownloadFile file)
        {
            return downloadService.TogglePlayPause(file);
        }

        /// <inheritdoc/>
        public async Task<(int torrentFileCount, int videoCount)> DownloadManyAsync(
            IEnumerable<ITreeNode> nodes, CancellationToken cancellationToken)
        {
            var torrentFileCount = await nodes
                .OfType<ITorrentTreeNode>()
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((t, ct) => new ValueTask<(IStorageFile?, DownloadResult result)>(
                    DownloadTorrentFileAsync(t, ct)))
                .AggregateAsync(0,
                    (l, r) => r.result == DownloadResult.InProgress || r.result == DownloadResult.Completed ? l + 1 : l,
                    cancellationToken)
                .ConfigureAwait(false);

            var videoCount = await nodes
                .OfType<File>()
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((f, ct) => f.GetDefaultAsync(ct))
                .Where(v => v != null)
                .SelectAwaitWithCancellation((v, ct) => new ValueTask<(IStorageFile?, DownloadResult result)>(
                    StartDownloadAsync(v!, ct)))
                .AggregateAsync(0,
                    (l, r) => r.result == DownloadResult.InProgress || r.result == DownloadResult.Completed ? l + 1 : l,
                    cancellationToken)
                .ConfigureAwait(false);

            return (torrentFileCount, videoCount);
        }

        /// <inheritdoc/>
        public async Task<(IStorageFile? file, DownloadResult result)> SaveManyAsPlaylistAsync(IEnumerable<ITreeNode> nodes, CancellationToken cancellationToken)
        {
            var playlist = new M3uPlaylist();
            playlist.IsExtended = true;

            var playlistEntries = nodes
                .Where(node => node is not IPreloadableNode preloadable || preloadable.IsPreloaded)
                .Select(node => node switch
                {
                    ITorrentTreeNode magnetTorrentNode
                    when magnetTorrentNode.IsMagnet && magnetTorrentNode.Link != null => (
                        title: magnetTorrentNode.Title,
                        link: magnetTorrentNode.Link),
                    ITorrentTreeNode torrentNode
                    when torrentNode.Link != null => (
                        title: torrentNode.Title,
                        link: new Uri(torrentNode.Link.ToString().Replace(torrentNode.Link.Scheme, "torrent"))),
                    File fileNode
                    when fileNode.GetByQuality(Settings.Instance.PreferredQuality, false) is Video video => (
                        title: GetFileNameFromVideo(video),
                        link: video.SingleLink),
                    _ => default
                })
                .Where(t => t.link != null)
                .Select(t => new M3uPlaylistEntry()
                {
                    Title = t.title,
                    Path = t.link!.ToString()
                });

            foreach (var entry in playlistEntries)
            {
                playlist.PlaylistEntries.Add(entry);
            }

            var content = new M3uContent();
            var text = content.ToText(playlist);

            var playlistFile = await storageService.PickFileAsync("playlist.m3u8", true).ConfigureAwait(false);
            if (playlistFile == null)
            {
                return (null, DownloadResult.FailedFileCreate);
            }
            var result = await playlistFile.WriteTextAsync(text).ConfigureAwait(false);
            if (!result)
            {
                return (null, DownloadResult.FailedUnknown);
            }

            logger.Log(LogLevel.Information, default, nodes.FirstOrDefault(), null, (_, __) => "SaveManyAsPlaylist");

            return (playlistFile, DownloadResult.Completed);
        }

        /// <inheritdoc/>
        public async Task<(IStorageFile? file, DownloadResult result)> StartDownloadAsync(
            Video video, CancellationToken cancellationToken)
        {
            if (video?.SingleLink == null)
            {
                return ((IStorageFile?)null, DownloadResult.NotSupportedMultiSource);
            }
            if (video.DownloadLink == null)
            {
                throw new NullReferenceException($"Attempt to download by null {nameof(video.DownloadLink)}");
            }

            var fileName = GetFileNameFromVideo(video);
            var subFolder = video.ParentFile?.Episode != null ? video.ParentFile.ItemTitle : null;

            var tupleResult = await DownloadInternalAsync(video.DownloadLink, fileName, video.ParentFile?.Id, video.Quality, subFolder, video.CustomHeaders, cancellationToken)
                .ConfigureAwait(false);

            if (tupleResult.result == DownloadResult.InProgress
                || tupleResult.result == DownloadResult.Completed)
            {
                logger.Log(LogLevel.Information, default, video.ParentFile, null, (_, __) => "VideoFileDownload");
            }

            return tupleResult;
        }

        /// <inheritdoc/>
        public async Task<(IStorageFile? file, DownloadResult result)> StartDownloadAsync(
            Uri link,
            string? fileName = null,
            Dictionary<string, string>? customHeaders = null,
            CancellationToken cancellationToken = default)
        {
            var tupleResult = await DownloadInternalAsync(link, fileName, null, null, null, customHeaders, cancellationToken).ConfigureAwait(false);

            if (tupleResult.result == DownloadResult.InProgress
                || tupleResult.result == DownloadResult.Completed)
            {
                logger.LogInformation("FileLinkDownload");
            }

            return tupleResult;
        }

        /// <inheritdoc/>
        public async Task<IStorageFolder?> GetVideosFolderAsync()
        {
            if (cachedFolder == null)
            {
                try
                {
                    var token = settingService.GetSetting(Settings.InternalSettingsContainer, VideoDownloadFolderKey, null);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        cachedFolder = await storageService.GetSavedFolderAsync(token!).ConfigureAwait(false);

                        if (cachedFolder == null)
                        {
                            storageService.ForgetSavedFolder(token!);
                            settingService.DeleteSetting(Settings.InternalSettingsContainer, VideoDownloadFolderKey);
                        }
                    }
                }
                catch (IOException ex)
                {
                    logger?.LogWarning(ex);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex);
                }
            }

            return cachedFolder;
        }

        /// <inheritdoc/>
        public async Task<IStorageFolder?> GetTorrentsFolderAsync()
        {
            if (cachedTorrentFolder == null)
            {
                try
                {
                    var token = settingService.GetSetting(Settings.InternalSettingsContainer, TorrentFilesDownloadFolderKey, null);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        cachedTorrentFolder = await storageService.GetSavedFolderAsync(token!).ConfigureAwait(false);

                        if (cachedTorrentFolder == null)
                        {
                            storageService.ForgetSavedFolder(token!);
                            settingService.DeleteSetting(Settings.InternalSettingsContainer, TorrentFilesDownloadFolderKey);
                        }
                    }
                }
                catch (IOException ex)
                {
                    logger?.LogWarning(ex);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex);
                }
            }

            return cachedTorrentFolder;
        }

        /// <inheritdoc/>
        public async Task<bool> PickVideosFolderAsync()
        {
            var folder = await storageService.PickFolderAsync().ConfigureAwait(false);
            if (folder == null)
            {
                return false;
            }

            cachedFolder = folder;
            var token = settingService.GetSetting(Settings.InternalSettingsContainer, VideoDownloadFolderKey, null);
            token = await storageService.SaveFolderAsync(cachedFolder, token).ConfigureAwait(false);
            settingService.SetSetting(Settings.InternalSettingsContainer, VideoDownloadFolderKey, token!);

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> PickTorrentsFolderAsync()
        {
            var folder = await storageService.PickFolderAsync().ConfigureAwait(false);
            if (folder == null)
            {
                return false;
            }

            cachedTorrentFolder = folder;
            var token = settingService.GetSetting(Settings.InternalSettingsContainer, TorrentFilesDownloadFolderKey, null);
            token = await storageService.SaveFolderAsync(cachedTorrentFolder, token).ConfigureAwait(false);
            settingService.SetSetting(Settings.InternalSettingsContainer, TorrentFilesDownloadFolderKey, token!);

            return true;
        }

        /// <inheritdoc/>
        public async Task<(IStorageFile? file, DownloadResult result)> DownloadTorrentFileAsync(
            ITorrentTreeNode torrent,
            CancellationToken cancellationToken)
        {
            if (torrent == null)
            {
                throw new ArgumentNullException(nameof(torrent));
            }

            var downloadResult = DownloadResult.Unknown;
            try
            {
                var folder = await GetTorrentsFolderAsync().ConfigureAwait(false);

                if (folder == null
                    && await PickTorrentsFolderAsync().ConfigureAwait(false))
                {
                    folder = await GetTorrentsFolderAsync().ConfigureAwait(false);
                }

                if (folder == null)
                {
                    return (null, downloadResult = DownloadResult.FailedFolderOpen);
                }

                await torrent.PreloadAsync(cancellationToken).ConfigureAwait(false);
                if (torrent.Link == null)
                {
                    return (null, downloadResult = DownloadResult.NotSupported);
                }

                if (torrent.IsMagnet)
                {
                    return (null, downloadResult = DownloadResult.NotSupportedMagnet);
                }

                var title = torrent.Title?.Split('\\', '/').LastOrDefault()
                    ?? torrent.Link.Segments.LastOrDefault()
                    ?? torrent.ItemInfo?.Title?.NotEmptyOrNull()
                    ?? "unknown";

                title = RemoveInvalidFileNameChars(title);

                if (title.Length > 64)
                {
                    title = title[..64];
                }

                if (!title.EndsWith("torrent", StringComparison.OrdinalIgnoreCase))
                {
                    title += ".torrent";
                }

                var file = await folder.CreateFileAsync(title, true).ConfigureAwait(false);

                using (var stream = await httpClient.GetBuilder(torrent.Link).SendAsync(cancellationToken).AsStream().ConfigureAwait(false))
                {
                    var success = file != null && stream != null
                        && await file.WriteAsync(stream).ConfigureAwait(false);

                    if (success)
                    {
                        return (file, downloadResult = DownloadResult.Completed);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
            finally
            {
                var properties = ((ILogState)torrent).GetLogProperties(false);
                properties["IsSuccess"] = (cancellationToken.IsCancellationRequested ? DownloadResult.Canceled : downloadResult).ToString();
                logger.Log(LogLevel.Information, default, properties, null, (_, __) => "TorrentFileDownloaded");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                downloadResult = DownloadResult.Canceled;
            }

            return (null, downloadResult);
        }

        /// <inheritdoc/>
        public async Task<bool> OpenTorrentAsync(
            ITorrentTreeNode torrent,
            NodeOpenWay way,
            CancellationToken cancellationToken)
        {
            if (torrent == null)
            {
                throw new ArgumentNullException(nameof(torrent));
            }

            var success = false;

            try
            {
                await torrent.PreloadAsync(cancellationToken).ConfigureAwait(false);

                if (torrent.Link == null)
                {
                    return false;
                }

                switch (way)
                {
                    case NodeOpenWay.Remote:
                        var remoteResult = await launcherService.RemoteLaunchUriAsync(new RemoteLaunchDialogInput(null, torrent.Link, false, null)).ConfigureAwait(false);
                        success = remoteResult.IsSuccess;
                        break;
                    case NodeOpenWay.CopyLink:
                        success = await shareService
                            .CopyTextToClipboardAsync(torrent.Link.ToString())
                            .ConfigureAwait(false);
                        break;
                    case NodeOpenWay.InBrowser when torrent.IsMagnet:
                    case NodeOpenWay.In3rdPartyApp when torrent.IsMagnet:
                        success = await launcherService.LaunchUriAsync(torrent.Link).ConfigureAwait(false) == LaunchResult.Success;
                        break;
                    case NodeOpenWay.In3rdPartyApp:
                        var (file, _) = await DownloadTorrentFileAsync(torrent, cancellationToken).ConfigureAwait(false);
                        if (file != null)
                        {
                            var launchResult = await launcherService.LaunchFileAsync(file).ConfigureAwait(false);

                            success = launchResult == LaunchResult.Success;
                        }
                        break;
                }

                return success;
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
            finally
            {
                var properties = ((ILogState)torrent).GetLogProperties(false);
                properties["OpenWay"] = way.ToString();
                properties["IsSuccess"] = success.ToString();
                logger.Log(LogLevel.Information, default, properties, null, (_, __) => "TorrentFileOpened");
            }

            return false;
        }

        private async Task<(IStorageFile? file, DownloadResult result)> DownloadInternalAsync(
            Uri link, string? fileName, string? fileId, int? videoQuality, string? subFolder, IDictionary<string, string>? customHeaders, CancellationToken cancellationToken)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = link.Segments.LastOrDefault() ?? link.ToString();
            }

            var headHeaders = (await httpClient
                .GetBuilder(link)
                .WithHeaders(customHeaders ?? new Dictionary<string, string>())
                .WithHeader("Range", "bytes=0-1")
                .SendAsync(HttpCompletionOption.ResponseHeadersRead, CancellationToken.None)
                .ConfigureAwait(false))?
                .Content?
                .Headers;

            if (fileName!.EndsWith(".", StringComparison.Ordinal)
                || !fileName.Contains('.'))
            {
                var ext = headHeaders?.ContentDisposition?.FileName?.Split('.').LastOrDefault()
                    ?? headHeaders?.ContentType?.MediaType?.Split('/').LastOrDefault()
                    ?? "mp4";
                if (!fileName.EndsWith(".", StringComparison.Ordinal))
                {
                    fileName += ".";
                }

                fileName += ext;
            }

            fileName = RemoveInvalidFileNameChars(fileName);

            if (Path.GetExtension(fileName).StartsWith(".m3u", StringComparison.OrdinalIgnoreCase)
                && !Settings.Instance.AllowDownloadM3U8Files)
            {
                return (null, DownloadResult.NotSupportedHls);
            }

            IStorageFile? file = null;

            var folder = await GetVideosFolderAsync().ConfigureAwait(false);

            if (folder == null
                && await PickVideosFolderAsync().ConfigureAwait(false))
            {
                folder = await GetVideosFolderAsync().ConfigureAwait(false);
            }

            if (folder == null)
            {
                return (null, DownloadResult.FailedFolderOpen);
            }

            if (subFolder != null)
            {
                subFolder = RemoveInvalidFileNameChars(subFolder);
                folder = await folder.GetOrCreateFolderAsync(subFolder).ConfigureAwait(false) ?? folder;
            }

            var fileNames = Array.Empty<string>();
            try
            {
                fileNames = (await folder.GetItemsAsync().ConfigureAwait(false)).Select(f => f.Title).ToArray();
            }
            catch (IOException ex)
            {
                logger?.LogWarning(ex);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }
            fileName = FileHelper.GetUniqueFileName(fileName, fileNames);

            try
            {
                file = await folder.CreateFileAsync(fileName, true).ConfigureAwait(false);
            }
            catch
            {
                // Ignore
            }
            if (file == null)
            {
                return (null, DownloadResult.FailedFileCreate);
            }

            var downloadFile = await downloadService.StartDownloadAsync(file, link, customHeaders, cancellationToken)
                .ConfigureAwait(false);

            if (downloadFile?.File == null)
            {
                return (file, DownloadResult.FailedUnknown);
            }
            var result = cancellationToken.IsCancellationRequested ? DownloadResult.Canceled
                : downloadFile.Status == DownloadStatus.Completed ? DownloadResult.Completed
                : downloadFile.Status == DownloadStatus.Error ? DownloadResult.FailedUnknown
                : DownloadResult.InProgress;

            if (result == DownloadResult.InProgress
                || result == DownloadResult.Completed)
            {
                if (downloadFile.TotalBytesToReceive == 0)
                {
                    downloadFile.TotalBytesToReceive = (ulong)(headHeaders?.ContentRange?.Length ?? 0);
                }

                try
                {
                    var entity = new DownloadEntity(
                        downloadFile.OperationId,
                        fileId,
                        videoQuality,
                        downloadFile.File.Path,
                        downloadFile.TotalBytesToReceive,
                        downloadFile.AddTime);
                    await downloadRepository.UpsertManyAsync(new[] { entity }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex);
                }
            }

            return (file, result);
        }

        private string GetFileNameFromVideo(Video video)
        {
            try
            {
                if (!video.HasValidFileName
                    && video.ParentFile != null)
                {
                    var status = video.ParentFile.ItemInfo?.Details.Status;

                    var seasonDigitsCount = (status?.CurrentSeason ?? video.ParentFile.Season ?? 0).DigitsCount();
                    var episodesDigitsCount = Math.Max(2, (status?.TotalEpisodes ?? status?.CurrentEpisode ?? ((video.ParentFile.Episode?.End.Value ?? 1) - 1)).DigitsCount());

                    var fileNameBuilder = new StringBuilder();

                    if (!string.IsNullOrWhiteSpace(video.ParentFile.ItemTitle))
                    {
                        fileNameBuilder.Append(video.ParentFile.ItemTitle);
                        if (video.ParentFile.Season.HasValue
                            || video.ParentFile.Episode.HasValue)
                        {
                            fileNameBuilder.Append(' ');
                        }
                    }

                    if (video.ParentFile.Season is int season)
                    {
                        fileNameBuilder.AppendFormat("s{0:D" + seasonDigitsCount + "}", season);
                    }

                    if (video.ParentFile.Episode is Range episode)
                    {
                        if (episode.HasRange())
                        {
                            fileNameBuilder.AppendFormat("e{0:D" + episodesDigitsCount + "}", episode.Start.Value);
                            fileNameBuilder.Append('-');
                            fileNameBuilder.AppendFormat("e{0:D" + episodesDigitsCount + "}", episode.End.Value - 1);
                        }
                        else
                        {
                            fileNameBuilder.AppendFormat("e{0:D" + episodesDigitsCount + "}", episode.Start.Value);
                        }
                    }

                    if (fileNameBuilder.Length == 0)
                    {
                        return video.FileName ?? string.Empty;
                    }

                    var linkFileName = Uri.UnescapeDataString(video.DownloadLink?.Segments.LastOrDefault()?.Split('?').First() ?? string.Empty);
                    var fileExt = linkFileName.Split('.').LastOrDefault();
                    if (fileExt?.Length > 1
                        && fileExt.Length < 5
                        && fileExt.GetLetters().Length > 0)
                    {
                        fileNameBuilder.Append('.');
                        fileNameBuilder.Append(fileExt);
                    }
                    else
                    {
                        // End with dot, we will get file type from head response
                        fileNameBuilder.Append('.');
                    }
                    return fileNameBuilder.ToString();
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }
            return video.FileName ?? string.Empty;
        }

        private static string RemoveInvalidFileNameChars(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Concat(fileName.Split(invalidChars));
        }

        private void DownloadService_DownloadProgressChanged(object sender, DownloadEventArgs e)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}

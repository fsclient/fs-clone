namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;

    public interface IDownloadManager
    {
        event EventHandler<EventArgs<IEnumerable<DownloadFile>>>? FilesRemoved;

        event EventHandler<DownloadEventArgs>? DownloadProgressChanged;

        Task<DownloadFile?> GetDownloadFileByVideo(Video video);

        Task<IEnumerable<DownloadFile>> GetDownloadsAsync(
            CancellationToken cancellationToken);

        Task RemoveFilesAsync(
            IEnumerable<DownloadFile> files, bool deleteFromDevice);

        Task TogglePlayPauseAsync(
            DownloadFile file);

        Task<(int torrentFileCount, int videoCount)> DownloadManyAsync(
            IEnumerable<ITreeNode> nodes, CancellationToken cancellationToken);

        Task<(IStorageFile? file, DownloadResult result)> SaveManyAsPlaylistAsync(
            IEnumerable<ITreeNode> nodes, CancellationToken cancellationToken);

        Task<(IStorageFile? file, DownloadResult result)> StartDownloadAsync(
            Video video, CancellationToken cancellationToken);

        Task<(IStorageFile? file, DownloadResult result)> StartDownloadAsync(
            Uri link, string? fileName = null, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

        Task<IStorageFolder?> GetVideosFolderAsync();

        Task<IStorageFolder?> GetTorrentsFolderAsync();

        Task<bool> PickVideosFolderAsync();

        Task<bool> PickTorrentsFolderAsync();

        Task<(IStorageFile? file, DownloadResult result)> DownloadTorrentFileAsync(
            ITorrentTreeNode torrent, CancellationToken cancellationToken);

        Task<bool> OpenTorrentAsync(
            ITorrentTreeNode torrent, NodeOpenWay way, CancellationToken cancellationToken);
    }
}

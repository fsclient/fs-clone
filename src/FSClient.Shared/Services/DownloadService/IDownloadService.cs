namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IDownloadService
    {
        event EventHandler<DownloadEventArgs>? DownloadProgressChanged;

        Task CancelDownloadAsync(DownloadFile file);

        Task TogglePlayPause(DownloadFile file);

        Task<IReadOnlyList<DownloadFile>> GetActiveDownloadsAsync();

        Task<DownloadFile?> StartDownloadAsync(
            IStorageFile file,
            Uri link,
            IDictionary<string, string>? customHeaders = null,
            CancellationToken cancellationToken = default);
    }
}

namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Foundation.Metadata;
    using Windows.Networking.BackgroundTransfer;

    using FSClient.Shared;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    public class BackgroundDownloadService : IDownloadService
    {
        private static readonly bool GetResponseInformationAvailable =
            ApiInformation.IsMethodPresent(typeof(DownloadOperation).Name,
                nameof(DownloadOperation.GetResponseInformation));

        private const string BackgroundTransferGroupName = "FS_downloads";

        private readonly ConcurrentDictionary<Guid, UWPDownloadFile> startedDownloads;

        private readonly Lazy<BackgroundDownloader?> Downloader;
        private readonly Lazy<BackgroundTransferGroup?> TransferGroup;

        private readonly ILogger logger;

        public BackgroundDownloadService(
            ILogger log)
        {
            logger = log;

            startedDownloads = new ConcurrentDictionary<Guid, UWPDownloadFile>();

            Downloader = new Lazy<BackgroundDownloader?>(DownloaderLazyInitializer);
            TransferGroup = new Lazy<BackgroundTransferGroup?>(TransferGroupLazyInitializer);

            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
        }

        /// <inheritdoc/>
        public event EventHandler<DownloadEventArgs>? DownloadProgressChanged;

        /// <inheritdoc/>
        public async Task<IReadOnlyList<DownloadFile>> GetActiveDownloadsAsync()
        {
            try
            {
                IEnumerable<DownloadOperation> downloads;
                if (TransferGroup.Value != null)
                {
                    downloads = (await Task
                            .WhenAll(
                                BackgroundDownloader.GetCurrentDownloadsAsync().AsTask(),
                                BackgroundDownloader.GetCurrentDownloadsForTransferGroupAsync(TransferGroup.Value)
                                    .AsTask())
                            .ConfigureAwait(false))
                        .SelectMany(d => d ?? Enumerable.Empty<DownloadOperation>());
                }
                else
                {
                    downloads = await BackgroundDownloader.GetCurrentDownloadsAsync();
                }

                foreach (var operation in downloads)
                {
                    if (!startedDownloads.TryGetValue(operation.Guid, out var file))
                    {
                        file = new UWPDownloadFile(operation, logger, operation.ResultFile.DateCreated);

                        if (file.Status != DownloadStatus.Completed)
                        {
                            AttachFile(file);
                            startedDownloads.TryAdd(file.OperationId, file);
                        }
                    }

                    UpdateProgress(file);
                }

                return startedDownloads.Values.Cast<DownloadFile>().ToList();
            }
            catch (Exception ex)
                when (ex.Message?.Contains("Quota for maximum number") == true
                      // User hasn't permissions
                      || unchecked((uint)ex.HResult) == 0x80072EE4)
            {
                logger?.LogWarning(ex);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }

            return new List<DownloadFile>();
        }

        /// <inheritdoc/>
        public Task CancelDownloadAsync(DownloadFile file)
        {
            if (file is UWPDownloadFile uwpDownloadFile)
            {
                uwpDownloadFile.CancellationTokenSource?.Cancel();
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<DownloadFile?> StartDownloadAsync(
            IStorageFile file,
            Uri link,
            IDictionary<string, string>? customHeaders = null,
            CancellationToken cancellationToken = default)
        {
            if (file is not UWPStorageFile uwpFile)
            {
                return null;
            }

            UWPDownloadFile downloadFile;
            CancellationTokenRegistration cancellation;
            try
            {
                customHeaders ??= new Dictionary<string, string>();
                var downloader = Downloader.Value;
                if (customHeaders.Count > 0
                    || downloader == null)
                {
                    downloader = new BackgroundDownloader {TransferGroup = TransferGroup.Value};
                    foreach (var header in customHeaders)
                    {
                        downloader.SetRequestHeader(header.Key, header.Value);
                    }
                }

                var operation = downloader.CreateDownload(link, uwpFile.File);
                downloadFile = new UWPDownloadFile(operation, logger, operation.ResultFile.DateCreated);
                cancellation = cancellationToken.Register(
                    state => ((CancellationTokenSource?)state)!.Cancel(),
                    downloadFile.CancellationTokenSource);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
                return null;
            }

            if (downloadFile.DownloadOperation == null)
            {
                return null;
            }

            downloadFile.Status = ConvertStatus(downloadFile.DownloadOperation.Progress.Status);

            try
            {
                startedDownloads.TryAdd(downloadFile.OperationId, downloadFile);
                _ = downloadFile.DownloadOperation.StartAsync()
                    .AsTask(
                        downloadFile.CancellationTokenSource!.Token,
                        new Progress<DownloadOperation>(_ =>
                        {
                            UpdateProgress(downloadFile);
                            cancellation.Dispose();
                        }))
                    .ContinueWith(resultTask => HandleDownloadEnded(downloadFile, resultTask), TaskScheduler.Default);

                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                UpdateProgress(downloadFile);
            }
            catch (OperationCanceledException)
            {
                DownloadProgressChanged?.Invoke(this, new DownloadEventArgs(downloadFile, DownloadStatus.Canceled));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);

                DownloadProgressChanged?.Invoke(this, new DownloadEventArgs(downloadFile, DownloadStatus.Error));
                return null;
            }

            if (downloadFile.DownloadOperation.Progress.BytesReceived > 0
                || downloadFile.DownloadOperation.Progress.Status == BackgroundTransferStatus.Running)
            {
                DownloadProgressChanged?.Invoke(this, new DownloadEventArgs(downloadFile, DownloadStatus.Running));
            }
            else
            {
                DownloadProgressChanged?.Invoke(this, new DownloadEventArgs(downloadFile, DownloadStatus.Idle));
            }

            return downloadFile;
        }

        /// <inheritdoc/>
        public Task TogglePlayPause(DownloadFile file)
        {
            try
            {
                if (file is UWPDownloadFile uwpDownloadFile
                    && uwpDownloadFile.DownloadOperation is DownloadOperation downloadOperation)
                {
                    if (file.Status == DownloadStatus.Running
                        || file.Status == DownloadStatus.Resuming)
                    {
                        downloadOperation.Pause();
                        file.Status = ConvertStatus(downloadOperation.Progress.Status);
                    }
                    else if (file.Status == DownloadStatus.Paused)
                    {
                        if (GetResponseInformationAvailable)
                        {
                            var actualLink = downloadOperation.GetResponseInformation()?.ActualUri;
                            if (downloadOperation.RequestedUri != actualLink)
                            {
                                // Ensure that we will resume operation with actual link
                                // because it will send new request to old RequestedUri, which could redirect
                                downloadOperation.RequestedUri = actualLink;
                            }
                        }

                        downloadOperation.Resume();
                        file.Status = DownloadStatus.Resuming;
                    }

                    return Task.CompletedTask;
                }
            }
            catch (Exception ex) when (!ex.Message.Contains("already"))
            {
                logger?.LogError(ex);
            }
            catch { }

            return Task.FromResult(false);
        }

        private void HandleDownloadEnded(UWPDownloadFile file, Task resultTask)
        {
            startedDownloads.TryRemove(file.OperationId, out _);

            if (resultTask.IsCanceled
                || file.CancellationTokenSource?.IsCancellationRequested == true)
            {
                DownloadProgressChanged?.Invoke(this, new DownloadEventArgs(file, DownloadStatus.Canceled));
            }
            else if (resultTask.IsFaulted)
            {
                resultTask.Exception?.Handle(ex =>
                {
                    if (ex is IOException
                        || ex is OperationCanceledException)
                    {
                        return true;
                    }

                    logger?.LogWarning(ex);
                    return true;
                });
                DownloadProgressChanged?.Invoke(this, new DownloadEventArgs(file, DownloadStatus.Error));
            }
            else if (resultTask.IsCompleted)
            {
                DownloadProgressChanged?.Invoke(this, new DownloadEventArgs(file, DownloadStatus.Completed));
            }
        }

        private BackgroundDownloader? DownloaderLazyInitializer()
        {
            try
            {
                return new BackgroundDownloader {TransferGroup = TransferGroup.Value};
            }
            catch (Exception ex)
            {
                ex.Data["Reason"] = "Exception when try to create BackgroundDownloader";
                logger?.LogWarning(ex);
                return null;
            }
        }

        private BackgroundTransferGroup? TransferGroupLazyInitializer()
        {
            try
            {
                var backgroundTransferGroup = BackgroundTransferGroup.CreateGroup(BackgroundTransferGroupName);
                backgroundTransferGroup.TransferBehavior = Settings.Instance.SerializedDownload
                    ? BackgroundTransferBehavior.Serialized
                    : BackgroundTransferBehavior.Parallel;
                return backgroundTransferGroup;
            }
            catch (Exception ex)
            {
                ex.Data["Reason"] = "Exception when try to create BackgroundTransferGroup";
                logger?.LogWarning(ex);
                return null;
            }
        }

        private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            try
            {
                if (args.PropertyName == nameof(Settings.Instance.SerializedDownload)
                    && TransferGroup.IsValueCreated
                    && TransferGroup.Value != null)
                {
                    TransferGroup.Value.TransferBehavior = Settings.Instance.SerializedDownload
                        ? BackgroundTransferBehavior.Serialized
                        : BackgroundTransferBehavior.Parallel;
                }
            }
            catch (Exception ex)
            {
                ex.Data["Reason"] = "Exception when try to set TransferGroup.TransferBehavior";
                logger?.LogWarning(ex);
            }
        }

        private void AttachFile(UWPDownloadFile file)
        {
            try
            {
                file.DownloadOperation.AttachAsync()
                    .AsTask(
                        file.CancellationTokenSource!.Token,
                        new Progress<DownloadOperation>(_ => UpdateProgress(file)))
                    .ContinueWith(r => HandleDownloadEnded(file, r), TaskScheduler.Default);
            }
            catch (OperationCanceledException)
            {
                DownloadProgressChanged?.Invoke(this, new DownloadEventArgs(file, DownloadStatus.Canceled));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);

                DownloadProgressChanged?.Invoke(this, new DownloadEventArgs(file, DownloadStatus.Error));
            }
        }

        private void UpdateProgress(UWPDownloadFile file)
        {
            try
            {
                if (file == null
                    || file.DownloadOperation is not DownloadOperation oper)
                {
                    return;
                }

                if (oper.Progress.TotalBytesToReceive > 0)
                {
                    file.TotalBytesToReceive = oper.Progress.TotalBytesToReceive;
                }
                else if (file.TotalBytesToReceive == 0
                         && oper.GetResponseInformation()?.Headers is var headers
                         && headers != null)
                {
                    file.TotalBytesToReceive = (ulong)WebHelper.GetContentSize(headers);
                }

                file.BytesReceived = oper.Progress.BytesReceived;
                file.Status = ConvertStatus(oper.Progress.Status);

                DownloadProgressChanged?.Invoke(this, new DownloadEventArgs(file, file.Status));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }
        }

        private static DownloadStatus ConvertStatus(BackgroundTransferStatus inputStatus)
        {
            switch (inputStatus)
            {
                case BackgroundTransferStatus.Running:
                    return DownloadStatus.Running;
                case BackgroundTransferStatus.PausedByApplication:
                    return DownloadStatus.Paused;
                case BackgroundTransferStatus.PausedCostedNetwork:
                case BackgroundTransferStatus.PausedNoNetwork:
                    return DownloadStatus.NoNetwork;
                case BackgroundTransferStatus.Error:
                    return DownloadStatus.Error;
                case BackgroundTransferStatus.Completed:
                    return DownloadStatus.Completed;
                case BackgroundTransferStatus.Idle:
                    return DownloadStatus.Idle;
                case BackgroundTransferStatus.Canceled:
                    return DownloadStatus.Canceled;
                default:
                    return DownloadStatus.Unknown;
            }
        }

        private class UWPDownloadFile : DownloadFile
        {
            public UWPDownloadFile(DownloadOperation operation, ILogger logger, DateTimeOffset addTime)
                : base(operation.Guid, new UWPStorageFile((Windows.Storage.StorageFile)operation.ResultFile, logger),
                    addTime)
            {
                DownloadOperation = operation;
                CancellationTokenSource = new CancellationTokenSource();
            }

            public override bool PauseSupported => base.PauseSupported
                                                   && DownloadOperation.GetResponseInformation()?.IsResumable == true;

            public DownloadOperation DownloadOperation { get; }

            public CancellationTokenSource? CancellationTokenSource { get; }
        }
    }
}

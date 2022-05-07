namespace FSClient.ViewModels.Items
{
    using System;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;

    public class DownloadFileViewModel : ViewModelBase
    {
        public DownloadFileViewModel(
            DownloadFile downloadFile,
            ILauncherService launcherService,
            IDownloadManager downloadManager,
            INotificationService notificationService)
        {
            DownloadFile = downloadFile;

            downloadManager.DownloadProgressChanged += DownloadManager_DownloadProgressChanged;

            PauseFileCommand = new AsyncCommand(
                (_) => downloadManager.TogglePlayPauseAsync(downloadFile),
                () => CanPause);

            RemoveFileCommand = new AsyncCommand(
                (_) => downloadManager.RemoveFilesAsync(new[] { downloadFile }, false));

            DeleteFileCommand = new AsyncCommand(
                (_) => downloadManager.RemoveFilesAsync(new[] { downloadFile }, true),
                () => downloadFile.Status != DownloadStatus.FileMissed);

            OpenFileCommand = new AsyncCommand(
                (_) => launcherService.LaunchFileAsync(downloadFile.File!),
                () => downloadFile.Status == DownloadStatus.Completed && downloadFile.File != null);

            OpenFolderCommand = new AsyncCommand(
                async (_) =>
                {
                    var folder = await downloadFile.File!.GetParentAsync().ConfigureAwait(false);
                    if (folder != null)
                    {
                        var result = await launcherService.LaunchFolderAsync(folder).ConfigureAwait(false);
                        if (result != LaunchResult.Success
                            && result.GetDisplayDescription() is string description)
                        {
                            await notificationService.ShowAsync(description, NotificationType.Error).ConfigureAwait(false);
                        }
                    }
                },
                () => downloadFile.File != null,
                AsyncCommandConflictBehaviour.Skip);
        }

        public AsyncCommand PauseFileCommand { get; }
        public AsyncCommand RemoveFileCommand { get; }
        public AsyncCommand DeleteFileCommand { get; }
        public AsyncCommand OpenFileCommand { get; }
        public AsyncCommand OpenFolderCommand { get; }

        internal DownloadFile DownloadFile { get; private set; }

        public Guid OperationId => DownloadFile.OperationId;

        public DateTimeOffset AddTime => DownloadFile.AddTime;

        public DownloadStatus Status => DownloadFile.Status;

        public string FileName => DownloadFile.FileName;

        public string? FilePath => DownloadFile.File?.Path;

        public bool PauseSupported => DownloadFile.PauseSupported;

        public bool CanPause => DownloadFile.Status == DownloadStatus.Paused
            || DownloadFile.Status == DownloadStatus.Running
            || DownloadFile.Status == DownloadStatus.Resuming;

        public int Progress => DownloadFile.TotalBytesToReceive == 0 ? 0
            : (int)(100 * (double)DownloadFile.BytesReceived / DownloadFile.TotalBytesToReceive);

        public bool IsIndeterminate => DownloadFile.TotalBytesToReceive == 0 && DownloadFile.Status == DownloadStatus.Running;

        public bool IsProgressActive => Progress < 100 &&
            (DownloadFile.Status == DownloadStatus.Paused
            || DownloadFile.Status == DownloadStatus.Running
            || DownloadFile.Status == DownloadStatus.Resuming
            || DownloadFile.Status == DownloadStatus.Idle
            || DownloadFile.Status == DownloadStatus.NoNetwork);

        public string StatusString
        {
            get
            {
                if (Progress == 100)
                {
                    return Strings.DownloadStatus_Completed;
                }

                return DownloadFile.Status switch
                {
                    DownloadStatus.Running when DownloadFile.TotalBytesToReceive > 0
                        => string.Format(Strings.DownloadStatus_Running_With_Total, DownloadFile.BytesReceived / 1000, DownloadFile.TotalBytesToReceive / 1000),
                    DownloadStatus.Running => string.Format(Strings.DownloadStatus_Running, DownloadFile.BytesReceived / 1000),
                    DownloadStatus.Paused => Strings.DownloadStatus_Paused,
                    DownloadStatus.NoNetwork => Strings.DownloadStatus_NoNetwork,
                    DownloadStatus.Error => Strings.DownloadStatus_Error,
                    DownloadStatus.FileMissed => Strings.DownloadStatus_FileMissed,
                    DownloadStatus.Completed => Strings.DownloadStatus_Completed,
                    DownloadStatus.Resuming => Strings.DownloadStatus_Resuming,
                    DownloadStatus.Idle when DownloadFile.TotalBytesToReceive > 0
                        => string.Format(Strings.DownloadStatus_Idle_With_Total, DownloadFile.BytesReceived / 1000, DownloadFile.TotalBytesToReceive / 1000),
                    DownloadStatus.Idle => Strings.DownloadStatus_Idle,
                    DownloadStatus.Canceled => Strings.DownloadStatus_Canceled,
                    _ => string.Empty,
                };
            }
        }

        private void DownloadManager_DownloadProgressChanged(object sender, DownloadEventArgs e)
        {
            if (DownloadFile.OperationId == e.File.OperationId)
            {
                DownloadFile = e.File;
                DownloadFile.Status = e.Status;

                PauseFileCommand.RaiseCanExecuteChanged();
                OpenFileCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(
                    nameof(Progress), nameof(CanPause), nameof(PauseSupported),
                    nameof(Status), nameof(StatusString), nameof(IsIndeterminate),
                    nameof(IsProgressActive));
            }
        }
    }
}

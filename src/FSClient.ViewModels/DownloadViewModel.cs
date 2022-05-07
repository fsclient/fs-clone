namespace FSClient.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;
    using FSClient.Shared.Services.Specifications;
    using FSClient.ViewModels.Abstract;
    using FSClient.ViewModels.Items;

    public class DownloadViewModel : SelectionViewModel
    {
        private ICollection<DownloadFileViewModel> downloadFiles;
        private DownloadFileSpecification? downloadFileSpecification;

        private readonly IDownloadManager downloadManager;
        private readonly ILauncherService launcherService;
        private readonly INotificationService notificationService;
        private readonly ISettingService settingService;

        public DownloadViewModel(
            IDownloadManager downloadManager,
            ILauncherService launcherService,
            INotificationService notificationService,
            ISettingService settingService)
        {
            downloadFiles = Array.Empty<DownloadFileViewModel>();
            this.downloadManager = downloadManager;
            this.launcherService = launcherService;
            this.notificationService = notificationService;
            this.settingService = settingService;

            OpenFolderCommand = new AsyncCommand(
                async (_) =>
                {
                    var folder = await downloadManager.GetVideosFolderAsync().ConfigureAwait(false);
                    if (folder == null)
                    {
                        await notificationService.ShowAsync(Strings.DownloadsViewModel_FolderIsNotSelected, NotificationType.Information).ConfigureAwait(false);
                        return;
                    }

                    var result = await launcherService.LaunchFolderAsync(folder).ConfigureAwait(false);
                    if (result != LaunchResult.Success
                        && result.GetDisplayDescription() is string description)
                    {
                        await notificationService.ShowAsync(description, NotificationType.Error).ConfigureAwait(false);
                    }
                },
                AsyncCommandConflictBehaviour.Skip);

            this.downloadManager.FilesRemoved += DownloadManager_FilesRemoved;
            this.downloadManager.DownloadProgressChanged += DownloadManager_DownloadProgressChanged;

            UpdateSourceCommand = new AsyncCommand(
                UpdateSourceAsync,
                AsyncCommandConflictBehaviour.Skip);

            RemoveSelectedCommand = new AsyncCommand(
                ct => downloadManager.RemoveFilesAsync(
                    SelectedItems.OfType<DownloadFileViewModel>().Select(vm => vm.DownloadFile), false),
                () => IsAnySelected,
                AsyncCommandConflictBehaviour.WaitPrevious);

            DeleteSelectedCommand = new AsyncCommand(
                ct => downloadManager.RemoveFilesAsync(
                    SelectedItems.OfType<DownloadFileViewModel>().Select(vm => vm.DownloadFile), true),
                () => IsAnySelected,
                AsyncCommandConflictBehaviour.WaitPrevious);
        }

        public IEnumerable<SortType> SortTypes { get; } = new[] {
            SortType.CreateDate,
            SortType.Alphabet,
            //SortType.FileSize
        };

        public SortType CurrentSortType
        {
            get => Get(() => settingService.GetSetting(Settings.StateSettingsContainer, "DownloadsSortType", SortTypes.First()));
            set
            {
                if (Set(value))
                {
                    settingService.SetSetting(Settings.StateSettingsContainer, "DownloadsSortType", (int)value);
                }
            }
        }

        public IEnumerable<object> Downloads
        {
            get => Get<IEnumerable<object>>();
            private set => Set(value);
        }

        public string SearchRequest
        {
            get => Get(string.Empty);
            set
            {
                if (Set(value))
                {
                    downloadFileSpecification = new DownloadFileSpecification(value);
                }
            }
        }

        public bool HasAnyDownload => downloadFiles.Count > 0;

        public AsyncCommand UpdateSourceCommand { get; }
        public AsyncCommand OpenFolderCommand { get; }
        public AsyncCommand RemoveSelectedCommand { get; }
        public AsyncCommand DeleteSelectedCommand { get; }

        public bool ShowBackgroundDownloadAlert
        {
            get => settingService.GetSetting(Settings.InternalSettingsContainer, nameof(ShowBackgroundDownloadAlert), true, SettingStrategy.Roaming);
            set => settingService.SetSetting(Settings.InternalSettingsContainer, nameof(ShowBackgroundDownloadAlert), value, SettingStrategy.Roaming);
        }

        protected override IEnumerable<object> Items => downloadFiles;

        public bool GroupItems
        {
            get => Get(() => settingService.GetSetting(Settings.StateSettingsContainer, "DownloadsGroupItems", true));
            set
            {
                if (Set(value))
                {
                    settingService.SetSetting(Settings.StateSettingsContainer, "DownloadsGroupItems", value);
                }
            }
        }

        protected override void OnIsAnySelectedChanged(bool newValue)
        {
            DeleteSelectedCommand.RaiseCanExecuteChanged();
            RemoveSelectedCommand.RaiseCanExecuteChanged();
        }

        private async Task UpdateSourceAsync(CancellationToken cancellationToken)
        {
            var downloads = await downloadManager.GetDownloadsAsync(cancellationToken)
               .ToArrayAsync()
               .ConfigureAwait(false);

            downloadFiles = downloads
                .Select(download => new DownloadFileViewModel(
                    download, launcherService, downloadManager, notificationService))
                .ToList();

            var enumerable = CurrentSortType switch
            {
                SortType.CreateDate => downloadFiles.OrderByDescending(f => f.AddTime),
                SortType.Alphabet => downloadFiles.OrderBy(f => f.FileName).ThenByDescending(f => f.AddTime),
                _ => downloadFiles.AsEnumerable()
            };

            if (downloadFileSpecification != null)
            {
                enumerable = enumerable
                    .Where(item => downloadFileSpecification.IsSatisfiedBy(item.DownloadFile));
            }

            if (GroupItems)
            {
                Downloads = enumerable.GroupBy(f => f.AddTime.GetElapsedTimeString()).ToList();
            }
            else
            {
                Downloads = enumerable.ToList();
            }

            OnPropertyChanged(nameof(HasAnyDownload));
        }

        private async void DownloadManager_DownloadProgressChanged(object sender, DownloadEventArgs e)
        {
            switch (e.Status)
            {
                case DownloadStatus.Canceled:
                    await notificationService.ShowAsync(string.Format(Strings.DownloadsViewModel_FileDownloadWasCancelled, e.File.FileName), NotificationType.Information)
                        .ConfigureAwait(false);
                    break;
                case DownloadStatus.Error:
                    await notificationService.ShowAsync(string.Format(Strings.DownloadsViewModel_FileDownloadFailed, e.File.FileName), NotificationType.Error)
                        .ConfigureAwait(false);
                    break;
                case DownloadStatus.Completed:
                    await notificationService.ShowAsync(string.Format(Strings.DownloadsViewModel_FileDownloadCompleted, e.File.FileName), NotificationType.Completed)
                        .ConfigureAwait(false);
                    break;
            }
        }

        private async void DownloadManager_FilesRemoved(object sender, EventArgs<IEnumerable<DownloadFile>> e)
        {
            var toRemove = downloadFiles.Where(f => e.Argument.Any(d => d.OperationId == f.OperationId)).ToArray();
            foreach (var file in toRemove)
            {
                downloadFiles.Remove(file);
            }
            await UpdateSourceCommand.ExecuteAsync(default).ConfigureAwait(false);
        }
    }
}

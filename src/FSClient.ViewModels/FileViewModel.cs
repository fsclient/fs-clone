namespace FSClient.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using FSClient.Localization.Resources;
    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;
    using FSClient.Shared.Services.Specifications;
    using FSClient.ViewModels.Abstract;

    public class FileViewModel : SelectionViewModel, IStateSaveable
    {
        private SearchTreeNodeSpecification? searchSpecification;

        private readonly SafeObservableCollection<ITreeNode> notFilteredFilesSource;
        private readonly SafeObservableCollection<ITreeNode> sequenseFilesSource;
        private readonly SafeObservableCollection<ITreeNode, IGrouping<string, ITreeNode>> groupedFilesSource;

        private readonly IFileManager fileManager;
        private readonly IFolderManager folderManager;
        private readonly IHistoryManager historyManager;
        private readonly IProviderManager providerManager;
        private readonly IDownloadManager downloadManager;
        private readonly IContentDialog<string, bool> confirmationDialog;
        private readonly INotificationService notificationService;
        private readonly ISettingService settingService;

        public FileViewModel(
            IFileManager fileManager,
            IFolderManager folderManager,
            IHistoryManager historyManager,
            IProviderManager providerManager,
            IDownloadManager downloadManager,
            INotificationService notificationService,
            ISettingService settingService,
            IShareService shareService,
            IContentDialog<string, bool> confirmationDialog)
        {
            this.notificationService = notificationService;
            this.settingService = settingService;

            this.downloadManager = downloadManager;
            this.historyManager = historyManager;
            this.providerManager = providerManager;
            this.confirmationDialog = confirmationDialog;
            this.folderManager = folderManager;
            this.fileManager = fileManager;
            this.fileManager.VideoOpened += async (video, _) =>
            {
                var file = video.ParentFile;

                if (file != null
                    && (CurrentFolder?.ItemsSource.Contains(file) != true)
                    && file.Parent is IFolderTreeNode parent)
                {
                    await SetupFolderAsync(parent).ConfigureAwait(false);
                }

                SelectedNode = file;
            };

            notFilteredFilesSource = new SafeObservableCollection<ITreeNode>();
            sequenseFilesSource = new SafeObservableCollection<ITreeNode>();
            groupedFilesSource = new SafeObservableCollection<ITreeNode, IGrouping<string, ITreeNode>>();

            UpdateProvidersCommand = new Command(UpdateProviders);

            WatchedSelectedToggleCommand = new Command(
                () => IsWatchedSelected = !IsWatchedSelected,
                () => IsAnySelected);

            OpenCommand = new AsyncCommand<ITreeNode>(
                (node, ct) => OpenNodeAsync(node, node is File ? Settings.FileOpenWay
                    : node is TorrentFolder && !Settings.TorrServerEnabled ? NodeOpenWay.In3rdPartyApp
                    : NodeOpenWay.InApp, ct),
                node => this.fileManager.IsOpenWayAvailableForNode(node, node is File ? Settings.FileOpenWay
                    : node is TorrentFolder && !Settings.TorrServerEnabled ? NodeOpenWay.In3rdPartyApp
                    : NodeOpenWay.InApp));

            OpenInBrowserCommand = new AsyncCommand<ITreeNode>(
                (node, ct) => OpenNodeAsync(node, NodeOpenWay.InBrowser, ct),
                node => this.fileManager.IsOpenWayAvailableForNode(node, NodeOpenWay.InBrowser),
                AsyncCommandConflictBehaviour.CancelPrevious);

            OpenRemoteCommand = new AsyncCommand<object>(
                (node, ct) => OpenNodeAsync(node, NodeOpenWay.Remote, ct),
                AsyncCommandConflictBehaviour.CancelPrevious);

            LoadRootCommand = new AsyncCommand(
                ct => LoadRootAsync(ct),
                () => CurrentItem != null,
                AsyncCommandConflictBehaviour.CancelPrevious);

            LoadLastCommand = new AsyncCommand(
                LoadLastAsync,
                () => CurrentItem != null,
                AsyncCommandConflictBehaviour.CancelPrevious);

            OpenVideoCommand = new AsyncCommand<Video>(
                OpenVideoAsync,
                v => v?.Links.Count > 0);

            RefreshFolderCommand = new AsyncCommand(
                RefreshFolderAsync,
                () => CurrentFolder != null,
                AsyncCommandConflictBehaviour.WaitPrevious);

            GoUpCommand = new AsyncCommand(
                GoUpAsync,
                () => CurrentFolder?.Parent != null,
                AsyncCommandConflictBehaviour.WaitPrevious);

            OpenTrailerCommand = new AsyncCommand(
                OpenTrailerAsync,
                () => CurrentItem != null,
                AsyncCommandConflictBehaviour.Skip);

            SearchInFolderCommand = new AsyncCommand<string>(
                SearchInFolderAsync,
                AsyncCommandConflictBehaviour.CancelPrevious);

            DownloadCommand = new AsyncCommand<object>(
                DownloadAsync,
                par => par switch
                {
                    Uri link => true,
                    Video video => true,
                    ITorrentTreeNode torrentFile => !torrentFile.IsMagnet,
                    File file => !file.IsFailed,
                    _ => false,
                });

            DownloadSelectedCommand = new AsyncCommand(
                DownloadSelectedAsync,
                () => IsAnySelected);

            SaveSelectedAsPlaylistCommand = new AsyncCommand(
                SaveSelectedAsPlaylistAsync,
                () => IsAnySelected);

            CopyLinkCommand = new AsyncCommand<object>(
                async (par, ct) =>
                {
                    var result = false;
                    if (par is Video vid && vid.Links.Count > 1)
                    {
                        var copyString = string.Join(Environment.NewLine, vid.Links.Select(l => l.ToString()));
                        result = await shareService.CopyTextToClipboardAsync(copyString).ConfigureAwait(false);
                    }
                    else
                    {
                        if (par is ITorrentTreeNode torrent)
                        {
                            await OpenNodeAsync(torrent, NodeOpenWay.CopyLink, ct).ConfigureAwait(false);
                        }
                        else
                        {
                            var link = (par as Video)?.SingleLink ?? (par as TorrentFolder)?.Link ?? par as Uri;
                            if (link != null)
                            {
                                result = await shareService.CopyTextToClipboardAsync(link.ToString()).ConfigureAwait(false);
                            }
                        }
                    }
                    if (result)
                    {
                        await this.notificationService
                            .ShowAsync(Strings.FilesViewModel_LinkCopiedClipboard, NotificationType.Information)
                            .ConfigureAwait(false);
                    }
                });

            SaveNodePositionCommand = new AsyncCommand<ITreeNode>(
                (node, _) => this.historyManager.UpsertAsync(new[] { node }),
                node => !string.IsNullOrEmpty(node?.Id));
        }

        protected override IEnumerable<object> Items => sequenseFilesSource;

        public IEnumerable<object> FilesSource => IsSourceGrouped
            ? (IEnumerable<object>)groupedFilesSource
            : sequenseFilesSource;

        public bool IsWatchedSelected
        {
            get => Get(false);
            set
            {
                if (Set(value))
                {
                    OnIsWatchedSelectedChanged(value);
                }
            }
        }

        public bool IsSourceGrouped
        {
            get => Get(false);
            private set
            {
                if (Set(value))
                {
                    OnPropertyChanged(nameof(FilesSource));
                }
            }
        }

        public HistoryItem? HistoryItem { get; set; }

        public ItemInfo? CurrentItem
        {
            get => Get<ItemInfo>();
            set
            {
                if (Set(value))
                {
                    LoadRootCommand.RaiseCanExecuteChanged();
                    LoadLastCommand.RaiseCanExecuteChanged();
                    OpenTrailerCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool UseAllProviders
        {
            get => Get(() => Settings.LoadAllSources);
            set
            {
                if (Set(value))
                {
                    if (value)
                    {
                        UseAnyProvider = false;
                        CurrentProvider = null;
                    }
                    else if (CurrentProvider == null)
                    {
                        UseAnyProvider = true;
                    }
                }
            }
        }

        public bool UseAnyProvider
        {
            get => Get(() => !Settings.LoadAllSources);
            set
            {
                if (Set(value))
                {
                    if (value)
                    {
                        UseAllProviders = false;
                        CurrentProvider = null;
                    }
                    else if (CurrentProvider == null)
                    {
                        UseAllProviders = true;
                    }
                }
            }
        }

        public bool TorrentsMode
        {
            get => Get(() => settingService.GetSetting(Settings.StateSettingsContainer, nameof(TorrentsMode), false));
            set
            {
                if (Set(value))
                {
                    settingService.SetSetting(Settings.StateSettingsContainer, nameof(TorrentsMode), value);

                    UpdateProviders();
                    RefreshFolderCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public Site? CurrentProvider
        {
            get => Get<Site?>();
            set
            {
                if (Set(value))
                {
                    if (value != null)
                    {
                        UseAllProviders = UseAnyProvider = false;
                    }
                    else if (!UseAllProviders && !UseAnyProvider)
                    {
                        if (Settings.LoadAllSources)
                        {
                            UseAllProviders = true;
                        }
                        else
                        {
                            UseAnyProvider = true;
                        }
                    }
                }
            }
        }

        public IEnumerable<Site> FileProviders
        {
            get => Get(Enumerable.Empty<Site>());
            private set => Set(value);
        }

        public ITreeNode? SelectedNode
        {
            get
            {
                if (CurrentFolder != null
                    && Get<ITreeNode>() is ITreeNode node
                    && CurrentFolder.ItemsSource.Contains(node))
                {
                    return node;
                }

                return null;
            }
            private set => Set(value);
        }

        public IFolderTreeNode? CurrentFolder => Get<IFolderTreeNode>();

        public bool ShowProvidersHelpInfo
        {
            get => settingService.GetSetting(
                Settings.InternalSettingsContainer, nameof(ShowProvidersHelpInfo), true, SettingStrategy.Roaming);
            set => settingService.SetSetting(
                Settings.InternalSettingsContainer, nameof(ShowProvidersHelpInfo), value, SettingStrategy.Roaming);
        }

        public Command UpdateProvidersCommand { get; }
        public Command WatchedSelectedToggleCommand { get; }
        public AsyncCommand GoUpCommand { get; }
        public AsyncCommand LoadRootCommand { get; }
        public AsyncCommand LoadLastCommand { get; }
        public AsyncCommand OpenTrailerCommand { get; }
        public AsyncCommand RefreshFolderCommand { get; }
        public AsyncCommand DownloadSelectedCommand { get; }
        public AsyncCommand SaveSelectedAsPlaylistCommand { get; }
        public AsyncCommand<string> SearchInFolderCommand { get; }
        public AsyncCommand<object> DownloadCommand { get; }
        public AsyncCommand<object> CopyLinkCommand { get; }
        public AsyncCommand<ITreeNode> OpenCommand { get; }
        public AsyncCommand<ITreeNode> OpenInBrowserCommand { get; }
        public AsyncCommand<object> OpenRemoteCommand { get; }
        public AsyncCommand<ITreeNode> SaveNodePositionCommand { get; }
        public AsyncCommand<Video> OpenVideoCommand { get; }

        public Uri? SaveStateToUri()
        {
            if (SelectedNode is ITreeNode node)
            {
                return UriParserHelper.GenerateUriFromNode(node, false);
            }

            return null;
        }

        protected override void OnSelectedItemsChanged(IEnumerable<object> newValues)
        {
            IsWatchedSelected = SelectedItems.OfType<ITreeNode>().All(n => n.IsWatched);
        }

        protected override void OnIsAnySelectedChanged(bool newValue)
        {
            DownloadSelectedCommand.RaiseCanExecuteChanged();
            WatchedSelectedToggleCommand.RaiseCanExecuteChanged();
        }

        private async void OnIsWatchedSelectedChanged(bool newValue)
        {
            if (selectionItemsChanging)
            {
                return;
            }

            await historyManager.UpsertAsync(SelectedItems.OfType<ITreeNode>()
                .OrderBy(node => node.Season)
                .ThenBy(node => node.Episode?.Start.Value)
                .Select(node =>
                {
                    node.IsWatched = newValue;
                    return node;
                }))
                .ConfigureAwait(false);
        }

        private async Task DownloadSelectedAsync(CancellationToken cancellationToken)
        {
            if (SelectedItems.Count == 1)
            {
                await DownloadAsync(SelectedItems[0], cancellationToken).ConfigureAwait(false);
                return;
            }

            var torrentFiles = SelectedItems.OfType<ITorrentTreeNode>().ToList();
            var onlineFiles = SelectedItems.Except(torrentFiles).OfType<File>().ToList();
            var folders = SelectedItems.Except(torrentFiles).OfType<Folder>().ToList();
            if (onlineFiles.Count > 5)
            {
                var result = await confirmationDialog
                    .ShowAsync(
                        Strings.FilesViewModel_ConfirmToManyFilesDownload,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (!result)
                {
                    return;
                }
            }
            if (folders.Count > 0)
            {
                await notificationService
                    .ShowAsync(Strings.FilesViewModel_FoldersDownloadingIsNotAvailable, NotificationType.Warning)
                    .ConfigureAwait(false);
                return;
            }

            using var preloadCTS = new CancellationTokenSource();
            if (onlineFiles.Count > 0)
            {
                await notificationService
                    .ShowClosableAsync(Strings.FilesViewModel_FilesPreloadingProcess, NotificationType.Information, preloadCTS.Token)
                    .ConfigureAwait(false);
                var result = await fileManager.PreloadNodesAsync(onlineFiles, true, null, cancellationToken).ConfigureAwait(false);

                if (!result)
                {
                    await notificationService
                        .ShowAsync(Strings.FilesViewModel_ErrorDuringFilesPreloading, NotificationType.Error)
                        .ConfigureAwait(false);
                }
            }

            var (torrentFileCount, videoCount) = await downloadManager
                .DownloadManyAsync(torrentFiles.OfType<ITreeNode>().Concat(onlineFiles), cancellationToken)
                .ConfigureAwait(false);
            preloadCTS.Cancel();

            if (torrentFiles.Count > 0)
            {
                await notificationService
                    .ShowAsync(string.Format(Strings.FilesViewModel_TorrentFilesDownloadCompleted, torrentFileCount), NotificationType.Information)
                    .ConfigureAwait(false);
            }
            if (onlineFiles.Count > 0)
            {
                await notificationService
                    .ShowAsync(string.Format(Strings.FilesViewModel_VideoFilesDownloadCompleted, videoCount), NotificationType.Information)
                    .ConfigureAwait(false);
            }
        }

        private async Task SaveSelectedAsPlaylistAsync(CancellationToken cancellationToken)
        {
            var torrentFiles = SelectedItems.OfType<ITorrentTreeNode>().ToList();
            var onlineFiles = SelectedItems.Except(torrentFiles).OfType<File>().ToList();
            var folders = SelectedItems.Except(torrentFiles).OfType<Folder>().ToList();
            if (folders.Count > 0)
            {
                await notificationService
                    .ShowAsync(Strings.FilesViewModel_FoldersDownloadingIsNotAvailable, NotificationType.Warning)
                    .ConfigureAwait(false);
                return;
            }

            using var preloadCTS = new CancellationTokenSource();
            if (onlineFiles.Count > 0)
            {
                await notificationService
                    .ShowClosableAsync(Strings.FilesViewModel_FilesPreloadingProcess, NotificationType.Information, preloadCTS.Token)
                    .ConfigureAwait(false);
                var preloadResult = await fileManager.PreloadNodesAsync(onlineFiles, true, null, cancellationToken).ConfigureAwait(false);

                if (!preloadResult)
                {
                    await notificationService
                        .ShowAsync(Strings.FilesViewModel_ErrorDuringFilesPreloading, NotificationType.Error)
                        .ConfigureAwait(false);
                }
            }

            preloadCTS.Cancel();

            var (file, result) = await downloadManager
                .SaveManyAsPlaylistAsync(torrentFiles.OfType<ITreeNode>().Concat(onlineFiles), cancellationToken)
                .ConfigureAwait(false);

            await ShowDownloadResultNotification(file, result).ConfigureAwait(false);
        }

        private async Task DownloadAsync(object parameter, CancellationToken cancellationToken)
        {
            var (file, result) = await (parameter switch
            {
                Uri link => downloadManager.StartDownloadAsync(link, null, null, cancellationToken),
                Video video => downloadManager.StartDownloadAsync(video, cancellationToken),
                ITorrentTreeNode torrent => downloadManager.DownloadTorrentFileAsync(torrent, cancellationToken),
                _ => Task.FromResult(((IStorageFile?)null, DownloadResult.NotSupported)),
            }).ConfigureAwait(false);

            await ShowDownloadResultNotification(file, result).ConfigureAwait(false);
        }

        private Task ShowDownloadResultNotification(IStorageFile? file, DownloadResult result)
        {
            return (file, result) switch
            {
                (_, DownloadResult.Canceled) =>
                    notificationService.ShowAsync(Strings.DownloadResult_Canceled, NotificationType.Information),
                (IStorageFile notNullFile, DownloadResult.Completed) =>
                    notificationService.ShowAsync(Strings.DownloadResult_Completed + Environment.NewLine + notNullFile.Path, NotificationType.Warning),
                (IStorageFile notNullFile, DownloadResult.InProgress) =>
                    notificationService.ShowAsync(Strings.DownloadResult_InProgress + Environment.NewLine + notNullFile.Title, NotificationType.Warning),
                (_, DownloadResult.NotSupportedMagnet) =>
                    notificationService.ShowAsync(Strings.DownloadResult_NotSupported_Magnet, NotificationType.Error),
                (_, DownloadResult.NotSupportedHls) =>
                    notificationService.ShowAsync(Strings.DownloadResult_NotSupported_Hls, NotificationType.Error),
                (_, DownloadResult.NotSupportedMultiSource) =>
                    notificationService.ShowAsync(Strings.DownloadResult_NotSupported_MultiSource, NotificationType.Error),
                (_, DownloadResult.NotSupported) =>
                    notificationService.ShowAsync(Strings.DownloadResult_NotSupported, NotificationType.Error),
                (_, DownloadResult.FailedFileCreate) =>
                    notificationService.ShowAsync(Strings.DownloadResult_FailedFileCreate, NotificationType.Error),
                (_, DownloadResult.FailedFolderOpen) =>
                    notificationService.ShowAsync(Strings.DownloadResult_FailedFolderOpen, NotificationType.Error),
                (_, DownloadResult.FailedUnknown) =>
                    notificationService.ShowAsync(Strings.DownloadResult_FailedUnknown, NotificationType.Error),
                _ => notificationService.ShowAsync(Strings.DownloadResult_Unknown, NotificationType.Warning)
            };
        }

        private void UpdateProviders()
        {
            var oldProvider = CurrentProvider;

            FileProviders = providerManager
                .GetFileProviders(TorrentsMode ? FileProviderTypes.Torrent : FileProviderTypes.Online)
                .Where(p => providerManager.IsProviderEnabled(p));

            if (CurrentProvider != null
                && oldProvider != null)
            {
                CurrentProvider = oldProvider;
            }
        }

        private async Task OpenTrailerAsync(CancellationToken cancellationToken)
        {
            if (CurrentItem == null)
            {
                return;
            }

            var trailers = await fileManager
                .GetTrailersAsync(CurrentItem, cancellationToken)
                .ToArrayAsync()
                .ConfigureAwait(false);
            if (trailers.Length == 0)
            {
                await notificationService.ShowAsync(Strings.FilesViewModel_NoTrailersFound, NotificationType.Warning).ConfigureAwait(false);
                return;
            }

            foreach (var trailer in trailers.OfType<File>())
            {
                var success = await fileManager
                    .OpenFileAsync(
                       trailer,
                       Settings.FileOpenWay,
                       cancellationToken)
                    .ConfigureAwait(false);
                if (success)
                {
                    return;
                }
            }

            await notificationService.ShowAsync(Strings.FilesViewModel_UnableToOpenTrailler, NotificationType.Warning).ConfigureAwait(false);
        }

        private async Task LoadLastAsync(CancellationToken cancellationToken)
        {
            CancelFolderLoadingCommands(LoadLastCommand);

            var success = await TryLoadLastFolderAsync(cancellationToken).ConfigureAwait(false);

            if (success
                && CurrentFolder != null
                && (CurrentFolder.ParentsEnumerable<IFolderTreeNode>().LastOrDefault() ?? CurrentFolder)?.Site is var lastParentSite)
            {
                UseAllProviders = lastParentSite == Site.All;
            }
            else
            {
                CurrentProvider = null;
                if (Settings.LoadAllSources)
                {
                    UseAllProviders = true;
                }
                else
                {
                    UseAnyProvider = true;
                }
            }

            if (!success)
            {
                await LoadRootAsync(cancellationToken, false).ConfigureAwait(false);
            }
        }

        private async Task<bool> OpenNodeAsync(object node, NodeOpenWay openWay, CancellationToken cancellationToken)
        {
            bool openNodeResult;
            switch (node)
            {
                case TorrentFolder torrent when openWay != NodeOpenWay.InApp:
                    openNodeResult = await downloadManager
                        .OpenTorrentAsync(torrent, openWay, cancellationToken)
                        .ConfigureAwait(false);

                    if (openWay == NodeOpenWay.CopyLink
                        && openNodeResult)
                    {
                        await notificationService
                            .ShowAsync(
                                Strings.FilesViewModel_LinkCopiedClipboard,
                                NotificationType.Information)
                            .ConfigureAwait(false);
                    }
                    else if (!openNodeResult)
                    {
                        await notificationService
                            .ShowAsync(
                                Strings.FilesViewModel_UnableToOpenTorrentFile,
                                NotificationType.Error)
                            .ConfigureAwait(false);
                    }
                    break;
                case Folder folder
                when openWay == NodeOpenWay.InApp:
                    CancelFolderLoadingCommands(OpenCommand);

                    ShowProgress = true;

                    var result = await OpenFolderNodeAsync(folder, cancellationToken).ConfigureAwait(false);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        ShowProgress = false;
                    }
                    openNodeResult = result == ProviderResult.Success;
                    break;
                case File file:
                    openNodeResult = await fileManager.OpenFileAsync(file, openWay, cancellationToken).ConfigureAwait(false);
                    if (openNodeResult && openWay == NodeOpenWay.CopyLink)
                    {
                        await notificationService
                            .ShowAsync(
                                Strings.FilesViewModel_LinkCopiedClipboard,
                                NotificationType.Completed)
                            .ConfigureAwait(false);
                    }
                    break;
                case Video video:
                    openNodeResult = await fileManager.OpenVideoAsync(video, openWay, cancellationToken).ConfigureAwait(false);
                    if (openNodeResult && openWay == NodeOpenWay.CopyLink)
                    {
                        await notificationService
                            .ShowAsync(
                                Strings.FilesViewModel_LinkCopiedClipboard,
                                NotificationType.Completed)
                            .ConfigureAwait(false);
                    }
                    break;
                case null:
                    openNodeResult = false;
                    break;
                default:
                    throw new NotSupportedException();
            }

            if (cancellationToken.IsCancellationRequested
                && (node is File || node is ITorrentTreeNode)
                && openWay == NodeOpenWay.InApp)
            {
                await notificationService
                    .ShowAsync(
                        Strings.FilesViewModel_FileOpeningWasCanceledByOtherOperation,
                        NotificationType.Warning)
                    .ConfigureAwait(false);
            }

            return openNodeResult;
        }

        private Task GoUpAsync(CancellationToken cancellationToken)
        {
            var folder = CurrentFolder?.Parent;
            if (folder == null)
            {
                return Task.CompletedTask;
            }

            CancelFolderLoadingCommands(GoUpCommand);

            return OpenFolderNodeAsync(folder, cancellationToken);
        }

        private async Task<ProviderResult> OpenFolderNodeAsync(IFolderTreeNode folder, CancellationToken cancellationToken)
        {
            ProviderResult result;
            if (folder is TorrentFolder
                && !Uri.TryCreate(Settings.TorrServerAddress, UriKind.Absolute, out _))
            {
                result = ProviderResult.Failed;
                await notificationService
                    .ShowAsync(Strings.FilesViewModel_InvalidTorrServerAddress, NotificationType.Error)
                    .ConfigureAwait(false);
            }
            else
            {
                result = await folderManager.OpenFolderAsync(folder, cancellationToken).ConfigureAwait(false);

                switch (result)
                {
                    case ProviderResult.Success:
                        await SetupFolderAsync(folder).ConfigureAwait(false);
                        break;
                    default:
                        await ShowErrorNotification(result, folder).ConfigureAwait(false);
                        break;
                }
            }
            return result;
        }

        private async Task OpenVideoAsync(Video video, CancellationToken cancellationToken)
        {
            await fileManager.OpenVideoAsync(video, Settings.FileOpenWay, cancellationToken).ConfigureAwait(false);
            if (Settings.FileOpenWay == NodeOpenWay.CopyLink)
            {
                await notificationService
                    .ShowAsync(
                        Strings.FilesViewModel_LinkCopiedClipboard,
                        NotificationType.Completed)
                    .ConfigureAwait(false);
            }
        }

        private async Task SetupFolderAsync(IFolderTreeNode? value, bool force = false)
        {
            var oldValue = CurrentFolder;
            if (Set(value, nameof(CurrentFolder)) || force)
            {
                GoUpCommand.RaiseCanExecuteChanged();
                RefreshFolderCommand.RaiseCanExecuteChanged();

                searchSpecification = null;
                notFilteredFilesSource.Clear();
                sequenseFilesSource.Clear();
                groupedFilesSource.Clear();
                if (oldValue != null)
                {
                    oldValue.CollectionChanged -= OnFolderChildrenChanged;
                }

                if (value == null)
                {
                    SelectedNode = null;
                    IsSourceGrouped = false;
                }
                else
                {
                    TorrentsMode = value.IsTorrent;

                    FillFilesSource(value.ToArray());

                    value.CollectionChanged += OnFolderChildrenChanged;

                    var child = await historyManager.GetLastViewedFolderChildAsync<ITreeNode>(value, HistoryItem).ConfigureAwait(false);
                    if (child != null)
                    {
                        SelectedNode = child;
                    }
                    else
                    {
                        SelectedNode = null;
                    }
                }
                OnPropertyChanged(nameof(FilesSource));
            }
        }

        private void OnFolderChildrenChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.Action == NotifyCollectionChangedAction.Add)
            {
                var newItems = args.NewItems.OfType<ITreeNode>();
                FillFilesSource(newItems.ToArray());
            }
        }

        private void FillFilesSource(ICollection<ITreeNode> newItems)
        {
            var isSourceGrouped = groupedFilesSource.Any() || newItems.Any(i => !string.IsNullOrWhiteSpace(i?.Group));

            notFilteredFilesSource.AddRange(newItems);
            sequenseFilesSource.AddRange(newItems.Where(FilterNode));
            if (isSourceGrouped)
            {
                groupedFilesSource.AddRange(newItems.Where(FilterNode).GroupBy(i => i.Group ?? Strings.Folders_NotInGroup));
            }

            IsSourceGrouped = isSourceGrouped;

            if (Settings.CanPreloadFiles)
            {
                var files = newItems.Where(FilterNode).OfType<File>().ToArray();
                _ = fileManager.PreloadNodesAsync(files, Settings.CanPreloadEpisodes, HistoryItem, CancellationToken.None);
            }
        }

        private bool FilterNode(ITreeNode node)
        {
            return searchSpecification?.IsSatisfiedBy(node) ?? true;
        }

        private async Task SearchInFolderAsync(string request, CancellationToken _)
        {
            if (string.IsNullOrWhiteSpace(request) && searchSpecification == null)
            {
                return;
            }

            searchSpecification = new SearchTreeNodeSpecification(request);
            sequenseFilesSource.Clear();
            groupedFilesSource.Clear();

            sequenseFilesSource.AddRange(notFilteredFilesSource.Where(FilterNode));
            if (IsSourceGrouped)
            {
                foreach (var group in notFilteredFilesSource.Where(FilterNode).GroupBy(i => i.Group ?? Strings.Folders_NotInGroup))
                {
                    groupedFilesSource.Add(group);
                    await Task.Yield();
                }
            }
            if (Settings.CanPreloadFiles)
            {
                var files = notFilteredFilesSource.Where(FilterNode).OfType<File>().ToArray();
                await fileManager.PreloadNodesAsync(files, Settings.CanPreloadEpisodes, HistoryItem, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private Task ShowErrorNotification(ProviderResult result, IFolderTreeNode? folder)
        {
            if (result != ProviderResult.Success
                && result != ProviderResult.Canceled
                && result.GetDisplayDescription() is string description)
            {
                return notificationService.ShowAsync(
                    string.Format(description, folder?.Site.Title ?? "'-'"),
                    result == ProviderResult.Failed ? NotificationType.Error : NotificationType.Warning);
            }

            return Task.CompletedTask;
        }

        private async Task<bool> TryLoadLastFolderAsync(CancellationToken cancellationToken)
        {
            if (CurrentItem == null)
            {
                return false;
            }

            ShowProgress = true;

            var (folder, correctHistoryItem) = await folderManager
                .GetFolderFromHistoryAsync(CurrentItem, HistoryItem, cancellationToken).ConfigureAwait(false);
            if (correctHistoryItem != null)
            {
                HistoryItem = correctHistoryItem;
            }

            await SetupFolderAsync(folder).ConfigureAwait(false);

            if (!cancellationToken.IsCancellationRequested)
            {
                ShowProgress = false;
            }

            if (HistoryItem?.AutoStart == true
                && SelectedNode is ITreeNode selectedNode)
            {
                await OpenNodeAsync(selectedNode, NodeOpenWay.InApp, cancellationToken).ConfigureAwait(false);
            }

            return CurrentFolder != null;
        }

        private async Task LoadRootAsync(CancellationToken cancellationToken, bool cancelOther = true)
        {
            if (CurrentItem == null)
            {
                return;
            }

            if (cancelOther)
            {
                CancelFolderLoadingCommands(LoadRootCommand);
            }

            ShowProgress = true;

            var provider = CurrentProvider ??
                (UseAllProviders || (!UseAnyProvider && Settings.LoadAllSources)
                    ? Site.All
                    : Site.Any);

            var (folder, result) = TorrentsMode
                ? await folderManager.GetTorrentsRootAsync(CurrentItem, provider, cancellationToken).ConfigureAwait(false)
                : await folderManager.GetFilesRootAsync(CurrentItem, provider, cancellationToken).ConfigureAwait(false);
            await SetupFolderAsync(folder).ConfigureAwait(false);

            if (result != ProviderResult.Success)
            {
                await ShowErrorNotification(result, folder).ConfigureAwait(false);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                ShowProgress = false;
            }
        }

        private async Task RefreshFolderAsync(CancellationToken cancellationToken)
        {
            if (CurrentFolder?.ItemInfo == null)
            {
                return;
            }

            CancelFolderLoadingCommands(RefreshFolderCommand);

            ShowProgress = true;

            var folder = await folderManager.ReloadFolderAsync(CurrentFolder, cancellationToken).ConfigureAwait(false);

            if (folder == null)
            {
                await notificationService.ShowAsync(Strings.FilesViewModel_ErrorDuringFolderRefresh, NotificationType.Error).ConfigureAwait(false);
            }
            else
            {
                await SetupFolderAsync(folder, force: true).ConfigureAwait(false);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                ShowProgress = false;
            }
        }

        private void CancelFolderLoadingCommands(ICommand? exceptCommand = null)
        {
            var folderLoadingCommands = new ICancelableCommand[]
            {
                OpenCommand,
                LoadRootCommand,
                LoadLastCommand,
                RefreshFolderCommand,
                GoUpCommand
            };
            foreach (var command in folderLoadingCommands)
            {
                if (command != exceptCommand)
                {
                    command.Cancel();
                }
            }
        }
    }
}

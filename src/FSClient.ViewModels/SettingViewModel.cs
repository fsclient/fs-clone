namespace FSClient.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;
    using FSClient.ViewModels.Items;
    using FSClient.ViewModels.Pages;

    using Humanizer;

    public class SettingViewModel : ViewModelBase
    {
        private const string SuggestedBackupName = "fs-backup-{0:yyyy-MM-dd-HH-mm}.json";
        private const string SkippedVersionSettingKey = "SkippedVersion";

        private readonly IBackupManager backupManager;
        private readonly IDownloadManager downloadManager;
        private readonly IFavoriteManager favoriteManager;

        private readonly ISettingService settingService;
        private readonly IAppLanguageService appLanguageService;
        private readonly IApplicationService applicationService;
        private readonly IStoreService storeService;
        private readonly IStorageService storageService;
        private readonly IAppInformation appInformation;
        private readonly ILauncherService launcherService;
        private readonly IVerificationService verificationService;
        private readonly INotificationService notificationService;
        private readonly IDatabaseContext databaseContext;

        private readonly IContentDialog<BackupDialogInput, BackupDialogOutput> backupDialog;
        private readonly IContentDialog<ChangesDialogInput, ChangesDialogOutput> changesDialog;
        private readonly IContentDialog<string, bool> confirmDialog;

        public SettingViewModel(
            IEnumerable<ISiteProvider> siteProviders,
            ISettingService settingService,
            IAppLanguageService appLanguageService,
            IUserManager userManager,
            IBackupManager backupManager, IProviderManager providerManager, IFavoriteManager favoriteManager, IDownloadManager downloadManager,
            IApplicationService applicationService,
            IStoreService storeService, IStorageService storageService, ILauncherService launcherService, INotificationService notificationService,
            IVerificationService verificationService,
            IProviderConfigService providerConfigService,
            IAppInformation appInformation,
            IDatabaseContext databaseContext,
            IContentDialog<BackupDialogInput, BackupDialogOutput> backupDialog,
            IContentDialog<ChangesDialogInput, ChangesDialogOutput> changesDialog,
            IContentDialog<string, bool> confirmDialog)
        {
            this.backupManager = backupManager;
            this.settingService = settingService;
            this.appLanguageService = appLanguageService;
            this.storageService = storageService;
            this.notificationService = notificationService;
            this.applicationService = applicationService;
            this.storeService = storeService;
            this.favoriteManager = favoriteManager;
            this.downloadManager = downloadManager;
            this.appInformation = appInformation;
            this.verificationService = verificationService;
            this.launcherService = launcherService;
            this.databaseContext = databaseContext;

            this.backupDialog = backupDialog;
            this.changesDialog = changesDialog;
            this.confirmDialog = confirmDialog;

            AuthUserViewModel = new AuthUserViewModel(userManager, notificationService);

            UpdateSourceCommand = new AsyncCommand(UpdateSourceAsync, AsyncCommandConflictBehaviour.Skip);
            SetVerifyEnabledCommand = new AsyncCommand<bool>((enabled, _) => SetVerifyEnabledAsync(enabled), AsyncCommandConflictBehaviour.WaitPrevious);
            ChooseFolderCommand = new AsyncCommand(_ => ChooseFolderAsync(), AsyncCommandConflictBehaviour.Skip);
            ChooseTorrentFolderCommand = new AsyncCommand(_ => ChooseTorrentFolderAsync(), AsyncCommandConflictBehaviour.Skip);
            ResetDataCommand = new AsyncCommand(ct => ResetUserDataAsync(ct), AsyncCommandConflictBehaviour.Skip);
            OpenLogsFolderCommand = new AsyncCommand(_ => OpenLogsFolderAsync(), AsyncCommandConflictBehaviour.Skip);
            OpenBackupTaskFolderCommand = new AsyncCommand(_ => OpenBackupTaskFolderAsync(), AsyncCommandConflictBehaviour.Skip);
            ShowBackupDialogCommand = new AsyncCommand(ShowBackupDialogAsync, AsyncCommandConflictBehaviour.Skip);
            ShowBackupLoadDialogCommand = new AsyncCommand(ShowBackupLoadDialogAsync, AsyncCommandConflictBehaviour.Skip);
            CheckForUpdatesCommand = new AsyncCommand<bool>(CheckForUpdatesAsync, AsyncCommandConflictBehaviour.Skip);
            ShowChangesDialogCommand = new AsyncCommand(ShowChangesDialogAsync, AsyncCommandConflictBehaviour.Skip);
            ChangeLogsCustomFolderCommand = new AsyncCommand(_ => ChangeLogsCustomFolderAsync(), AsyncCommandConflictBehaviour.Skip);
            ChangeBackupTaskCustomFolderCommand = new AsyncCommand(_ => ChangeBackupTaskCustomFolderAsync(), AsyncCommandConflictBehaviour.Skip);

            favoriteManager.FavoritesChanged += (s, a) =>
            {
                if (a.Reason == FavoriteItemChangedReason.Reset)
                {
                    OnPropertyChanged(nameof(UseOnlineFavorites));
                }
            };

            Settings.PropertyChanged += (s, a) =>
            {
                if (a.PropertyName == nameof(Settings.MainSite))
                {
                    OnPropertyChanged(nameof(MainSite));
                    AuthUserViewModel.SetSiteCommand.Execute(Settings.MainSite);
                }
            };

            Pages = Enum
                .GetValues(typeof(SettingType))
                .Cast<SettingType>()
                .Select(t => new SettingPageViewModel { SettingType = t, ViewModel = this })
                .ToArray();

            Languages = appLanguageService.GetAvailableLanguages()
                .Select(l => new CultureInfo(l).TwoLetterISOLanguageName)
                .ToArray();

            var providers = siteProviders.ToDictionary(p => p.Site, p => p);
            Providers = new ObservableCollection<ProviderViewModel>(providerManager.GetOrderedProviders()
                .Select(site => (
                    valid: providers.TryGetValue(site, out var siteProvider),
                    siteProvider
                ))
                .Where(t => t.valid && t.siteProvider!.IsVisibleToUser)
                .Select(t => new ProviderViewModel(providerManager, providerConfigService, userManager, t.siteProvider!, notificationService)));
            Providers.CollectionChanged += (s, a) => providerManager.SetProvidersOrder(Providers.Select(p => p.Site));

            AvailableSites = providerManager.GetMainProviders();

            if (Secrets.SupportEmailAddress != null)
            {
                MailToLink = new Uri($"mailto:{Secrets.SupportEmailAddress}?subject=FSClient&"
                    + "body=%0D%0A%0D%0A_________________________________%0D%0A"
                    + appInformation.ToString()!.Replace(Environment.NewLine, "%0D%0A"));
            }
        }

        public AuthUserViewModel AuthUserViewModel { get; }

        public AsyncCommand<bool> SetVerifyEnabledCommand { get; }
        public AsyncCommand<bool> CheckForUpdatesCommand { get; }
        public AsyncCommand ShowChangesDialogCommand { get; }
        public AsyncCommand UpdateSourceCommand { get; }
        public AsyncCommand ResetDataCommand { get; }
        public AsyncCommand OpenLogsFolderCommand { get; }
        public AsyncCommand OpenBackupTaskFolderCommand { get; }
        public AsyncCommand ChangeLogsCustomFolderCommand { get; }
        public AsyncCommand ChangeBackupTaskCustomFolderCommand { get; }
        public AsyncCommand ChooseFolderCommand { get; }
        public AsyncCommand ChooseTorrentFolderCommand { get; }
        public AsyncCommand ShowBackupDialogCommand { get; }
        public AsyncCommand ShowBackupLoadDialogCommand { get; }

        public string AppVersion => GetAppVersionString();

        public bool ShowDevSettings =>
#if DEV_BUILD
            true;
#else
            false;
#endif

        public Uri? MailToLink { get; }

        public Uri PrivatePolicyLink => applicationService.PrivacyInfoLink;

        public Uri FAQLink => applicationService.FAQLink;

        public ulong? AvailableSpace
        {
            get => Get<ulong?>();
            private set => Set(value);
        }

        public string? ChosenFolderName
        {
            get => Get<string>() ?? Strings.SettingsViewModel_ChooseFolder;
            private set => Set(value);
        }

        public string? ChosenTorrentFolderName
        {
            get => Get<string>() ?? Strings.SettingsViewModel_ChooseFolder;
            private set => Set(value);
        }

        public bool UseOnlineFavorites
        {
            get => favoriteManager.ProviderType == FavoriteProviderType.Online;
            set => favoriteManager.ProviderType = value
                    ? FavoriteProviderType.Online
                    : FavoriteProviderType.Local;
        }

        public IEnumerable<NavigationPageType> StartPages { get; } = new[]
        {
            NavigationPageType.Home, NavigationPageType.Search, NavigationPageType.Favorites, NavigationPageType.History, NavigationPageType.LastWatched
        };

        public NavigationPageType StartPage
        {
            get => Settings.StartPage;
            set => Settings.StartPage = value;
        }

        public IEnumerable<Quality> AvailableQualities => new Quality[]
        {
            1440, 1080, 720, 480, 360
        };

        public Quality PreferredQuality
        {
            get => Settings.PreferredQuality;
            set => Settings.PreferredQuality = value;
        }

        public IEnumerable<NodeOpenWay> FileOpenWays { get; } =
            new[] { NodeOpenWay.InApp, NodeOpenWay.InSeparatedWindow, NodeOpenWay.InBrowser, NodeOpenWay.Remote, NodeOpenWay.CopyLink, NodeOpenWay.In3rdPartyApp };

        public NodeOpenWay FileOpenWay
        {
            get => Settings.FileOpenWay;
            set => Settings.FileOpenWay = value;
        }

        public IEnumerable<Site> AvailableSites { get; }

        public Site MainSite
        {
            get => Settings.MainSite;
            set => Settings.MainSite = value;
        }

        public IEnumerable<ClearCacheModes> ClearCacheModes { get; } =
            Enum.GetValues(typeof(ClearCacheModes)).Cast<ClearCacheModes>();

        public ClearCacheModes ClearCacheMode
        {
            get => Settings.ClearCacheMode;
            set => Settings.ClearCacheMode = value;
        }

        public IEnumerable<SettingPageViewModel> Pages { get; }

        public IEnumerable<string> Languages { get; }

        public string CurrentLanguage
        {
            get => Get(() => new CultureInfo(appLanguageService.GetCurrentLanguage()).TwoLetterISOLanguageName);
            set
            {
                if (value != null
                    && Set(value))
                {
                    var code = value;
                    Settings.Instance.CurrentLanguageISOCode = code;
                    _ = appLanguageService.ApplyLanguageAsync(code).AsTask();
                }
            }
        }

        public ObservableCollection<ProviderViewModel> Providers { get; }

        public static string? GetLanguageFriendlyName(string? code)
        {
            return code == null ? null : new CultureInfo(code).NativeName.ToLower();
        }

        private string GetAppVersionString()
        {
            var assemblyVersion = appInformation.AssemblyVersion.ToString(3);
            var manifsetVersion = appInformation.ManifestVersion.ToString(3);
            if (manifsetVersion == assemblyVersion)
            {
                return $"v{assemblyVersion}";
            }
            else
            {
                return $"v{manifsetVersion} (build v{assemblyVersion})";
            }
        }

        private Task UpdateSourceAsync(CancellationToken cancellationToken)
        {
            return Task
                .WhenAll(
                    AuthUserViewModel.SetSiteCommand.ExecuteAsync(MainSite, cancellationToken),
                    UpdateFolderInfoAsync());
        }

        private async Task ChooseFolderAsync()
        {
            await downloadManager.PickVideosFolderAsync().ConfigureAwait(false);
            await UpdateFolderInfoAsync().ConfigureAwait(false);
        }

        private async Task ChooseTorrentFolderAsync()
        {
            await downloadManager.PickTorrentsFolderAsync().ConfigureAwait(false);
            await UpdateFolderInfoAsync().ConfigureAwait(false);
        }

        private async Task UpdateFolderInfoAsync()
        {
            var folder = await downloadManager.GetVideosFolderAsync().ConfigureAwait(false);
            var torrentfolder = await downloadManager.GetTorrentsFolderAsync().ConfigureAwait(false);

            ChosenFolderName = folder?.Title;
            AvailableSpace = folder != null ? await folder.GetAvaliableSpaceAsync().ConfigureAwait(false) / 1024 / 1024 : null;

            ChosenTorrentFolderName = torrentfolder?.Title;
        }

        private async Task ResetUserDataAsync(CancellationToken cancellationToken)
        {
            var result = await confirmDialog.ShowAsync(Strings.SettingsViewModel_ResetUserDataConfirmation, cancellationToken).ConfigureAwait(false);
            if (result)
            {
                await databaseContext.DropAsync().ConfigureAwait(false);
                await storageService.ClearApplicationData().ConfigureAwait(false);
                await notificationService.ShowAsync(Strings.SettingsViewModel_ResetUserDataCompleted, NotificationType.Completed).ConfigureAwait(false);
            }
        }

        private async Task OpenLogsFolderAsync()
        {
            IStorageFolder? folder = null;
            if (Settings.Instance.FileLoggerCustomFolder is string customFolder)
            {
                folder = await storageService.GetSavedFolderAsync(customFolder).ConfigureAwait(false);
            }
            if (folder == null)
            {
                folder = storageService.LocalFolder == null ? null
                    : await storageService.LocalFolder.GetOrCreateFolderAsync(StorageServiceExtensions.LogsFolderName).ConfigureAwait(false);
            }

            if (folder == null)
            {
                await notificationService.ShowAsync(Strings.SettingsViewModel_FolderWasNotFound, NotificationType.Information).ConfigureAwait(false);
                return;
            }

            var result = await launcherService.LaunchFolderAsync(folder).ConfigureAwait(false);
            if (result != LaunchResult.Success
                && result.GetDisplayDescription() is string description)
            {
                await notificationService.ShowAsync(description, NotificationType.Error).ConfigureAwait(false);
            }
        }

        private async Task OpenBackupTaskFolderAsync()
        {
            IStorageFolder? folder = null;
            if (Settings.Instance.AutomatedBackupTaskCustomFolder is string customFolder)
            {
                folder = await storageService.GetSavedFolderAsync(customFolder).ConfigureAwait(false);
            }
            if (folder == null)
            {
                folder = storageService.LocalFolder == null ? null
                    : await storageService.LocalFolder.GetOrCreateFolderAsync(StorageServiceExtensions.BackupFolderName).ConfigureAwait(false);
            }
            if (folder == null)
            {
                await notificationService.ShowAsync(Strings.SettingsViewModel_FolderWasNotFound, NotificationType.Information).ConfigureAwait(false);
                return;
            }

            var result = await launcherService.LaunchFolderAsync(folder).ConfigureAwait(false);
            if (result != LaunchResult.Success
                && result.GetDisplayDescription() is string description)
            {
                await notificationService.ShowAsync(description, NotificationType.Error).ConfigureAwait(false);
            }
        }

        private async Task ShowBackupDialogAsync(CancellationToken cancellationToken)
        {
            var modalOutput = await backupDialog
                .ShowAsync(new BackupDialogInput(Strings.SettingsViewModel_BackupTypeSelectionExportHeader), cancellationToken).ConfigureAwait(false);
            if (modalOutput.SelectedTypes == BackupDataTypes.None
                || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var backupData = await backupManager.BackupAsync(modalOutput.SelectedTypes, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var suggestedName = string.Format(CultureInfo.InvariantCulture, SuggestedBackupName, DateTime.Now);
            var file = await storageService.PickFileAsync(suggestedName, true).ConfigureAwait(false);
            if (file == null)
            {
                await notificationService.ShowAsync(Strings.SettingsViewModel_UnableToCreateFile, NotificationType.Error).ConfigureAwait(false);
                return;
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var result = await file.WriteJsonAsync(backupData, cancellationToken).ConfigureAwait(false);
            if (!result)
            {
                await notificationService.ShowAsync(Strings.SettingsViewModel_UnableToWriteFile, NotificationType.Error).ConfigureAwait(false);
                return;
            }

            await notificationService.ShowAsync(Strings.SettingsViewModel_BackupSuccessfully, NotificationType.Completed).ConfigureAwait(false);

            var message = string.Format(CultureInfo.InvariantCulture,
                Strings.SettingsViewModel_BackupExportResultsMessage,
                backupData.Favorites.Count,
                backupData.History.Count,
                backupData.Settings.SelectMany(str =>
                    str.Value.SelectMany(cont => cont.Value)).Count());

            await notificationService.ShowAsync(message, NotificationType.Completed).ConfigureAwait(false);
        }

        private async Task ShowBackupLoadDialogAsync(CancellationToken cancellationToken)
        {
            var file = await storageService.PickFileAsync(SuggestedBackupName).ConfigureAwait(false);
            if (file == null
                || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var backupData = await file.ReadFromJsonFileAsync<BackupData>(cancellationToken).ConfigureAwait(false);
            if (backupData == null)
            {
                await notificationService.ShowAsync(Strings.SettingsViewModel_UnableToReadFile, NotificationType.Error).ConfigureAwait(false);
                return;
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var possibleTypes = backupManager.GetPossibleTypes(backupData);

            var modalOutput = await backupDialog.ShowAsync(new BackupDialogInput(Strings.SettingsViewModel_BackupTypeSelectionImportHeader, possibleTypes), cancellationToken).ConfigureAwait(false);
            if (modalOutput.SelectedTypes == BackupDataTypes.None
                || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            using var waitNotificationCTS = new CancellationTokenSource();
            await notificationService.ShowClosableAsync(Strings.SettingsViewModel_BackupImportInProcess, NotificationType.Information, waitNotificationCTS.Token).ConfigureAwait(false);
            var result = await backupManager.RestoreFromBackupAsync(backupData, modalOutput.SelectedTypes, cancellationToken).ConfigureAwait(false);
            waitNotificationCTS.Cancel();

            var message = string.Format(CultureInfo.InvariantCulture,
                Strings.SettingsViewModel_BackupImportResultsMessage,
                result.FavoritesRestoredCount, result.HistoryRestoredCount, result.SettingsRestoredCount);

            await notificationService.ShowAsync(message, NotificationType.Completed).ConfigureAwait(false);

            if (result.FavoritesRestoredCount > 0 || result.SettingsRestoredCount > 0)
            {
                await notificationService.ShowAsync(Strings.SettingsViewModel_RebootApplication, NotificationType.Information).ConfigureAwait(false);
            }
        }

        private async Task CheckForUpdatesAsync(bool forceCheckForUpdate, CancellationToken cancellationToken)
        {
            if (forceCheckForUpdate)
            {
                await notificationService.ShowAsync(Strings.SettingsViewModel_CheckForUpdatesInProgress, NotificationType.Information).ConfigureAwait(false);
            }
            var checkForUpdatesResult = await storeService.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);

            var skippedVersion = settingService.GetSetting(Settings.StateSettingsContainer, SkippedVersionSettingKey, null, SettingStrategy.Local);
            if (!forceCheckForUpdate
                && skippedVersion != null
                && checkForUpdatesResult.Version?.ToString(3) == skippedVersion)
            {
                checkForUpdatesResult = new CheckForUpdatesResult(CheckForUpdatesResultType.Skipped, checkForUpdatesResult.Version, null);
            }

            if (forceCheckForUpdate
                && checkForUpdatesResult.ResultType.GetDisplayDescription() is string description
                && checkForUpdatesResult.ResultType != CheckForUpdatesResultType.Available)
            {
                await notificationService.ShowAsync(description, NotificationType.Information).ConfigureAwait(false);
            }

            if (!appInformation.IsUpdated
                && !forceCheckForUpdate
                && checkForUpdatesResult.ResultType != CheckForUpdatesResultType.Available)
            {
                // Application wasn't updated and there is no any other updates available.
                return;
            }

            var changelog = await applicationService.GetChangelogAsync(cancellationToken).ConfigureAwait(false);
            var currentVersion = appInformation.ManifestVersion;
            var isUpdated = appInformation.IsUpdated;
            var hasUpdate = checkForUpdatesResult.ResultType == CheckForUpdatesResultType.Available;
            var currentVersionChanges = !isUpdated ? null : changelog.FirstOrDefault(e => e.Version != null
                && e.Version.Major == currentVersion.Major
                && e.Version.Minor == currentVersion.Minor
                && e.Version.Build == currentVersion.Build);
            var shouldShowCurrentUpdate = currentVersionChanges != null
                && (currentVersionChanges.ShowOnStartup ?? true)
                && Settings.Instance.CanShowUpdateChangeLog;

            if (forceCheckForUpdate
                || hasUpdate
                || shouldShowCurrentUpdate)
            {
                var input = new ChangesDialogInput(changelog)
                {
                    ShowToVersion = checkForUpdatesResult.Version ?? currentVersion,
                    ShowFromVersion = isUpdated || hasUpdate ? currentVersion : null,
                    UpdateVersion = checkForUpdatesResult.Version
                };

                if (checkForUpdatesResult.ResultType == CheckForUpdatesResultType.Skipped)
                {
                    input = input with
                    {
                        ShowToVersion = currentVersion,
                        UpdateVersion = null
                    };
                }
                var result = await changesDialog.ShowAsync(input, cancellationToken).ConfigureAwait(false);

                if (checkForUpdatesResult.InstallUpdateLink is { } installUpdateLink)
                {
                    if (result.ShouldOpenUpdatePage)
                    {
                        var launchResult = await launcherService.LaunchUriAsync(installUpdateLink).ConfigureAwait(false);

                        if (launchResult != LaunchResult.Success
                            && launchResult.GetDisplayDescription() is string launchResultDescription)
                        {
                            await notificationService.ShowAsync(launchResultDescription, NotificationType.Error).ConfigureAwait(false);
                        }
                    }
                    else if (checkForUpdatesResult.Version is { } versionToSkip
                        && !forceCheckForUpdate)
                    {
                        var confirmationMessage = Strings.ChangesDialog_SkipUpdateToNewVersion.FormatWith(versionToSkip);
                        var shouldSkipNextVersion = await confirmDialog.ShowAsync(confirmationMessage, cancellationToken).ConfigureAwait(false);
                        if (shouldSkipNextVersion)
                        {
                            settingService.SetSetting(Settings.StateSettingsContainer, SkippedVersionSettingKey, versionToSkip.ToString(3), SettingStrategy.Local);
                        }
                    }
                }
            }
            else if (isUpdated)
            {
                await notificationService.ShowAsync(Strings.Activation_ApplicationUpdated, NotificationType.Information).ConfigureAwait(true);
            }
        }

        private async Task ShowChangesDialogAsync(CancellationToken cancellationToken)
        {
            var changelog = await applicationService.GetChangelogAsync(cancellationToken).ConfigureAwait(false);
            var currentVersion = appInformation.ManifestVersion;
            var input = new ChangesDialogInput(changelog)
            {
                ShowToVersion = currentVersion
            };
            await changesDialog.ShowAsync(input, cancellationToken).ConfigureAwait(false);
        }

        private async Task SetVerifyEnabledAsync(bool verifyEnabled)
        {
            if (verifyEnabled == Settings.VerifyAccess)
            {
                return;
            }

            var isAvailable = await verificationService.IsAvailableAsync().ConfigureAwait(true);
            if (!isAvailable)
            {
                Settings.VerifyAccess = false;
                await notificationService.ShowAsync(
                        Strings.SettingsViewModel_AccessVerificationIsNotAvailable,
                        NotificationType.Warning)
                    .ConfigureAwait(false);
                return;
            }

            if (!verifyEnabled)
            {
                Settings.VerifyAccess = false;
            }
            else
            {
                var (success, message) = await verificationService.RequestVerificationAsync().ConfigureAwait(false);
                if (success)
                {
                    Settings.VerifyAccess = true;
                }
                else
                {
                    Settings.VerifyAccess = false;
                    await notificationService.ShowAsync(Strings.SettingsViewModel_AccessVerificationFailed + message, NotificationType.Error).ConfigureAwait(false);
                }
            }
        }

        private async Task ChangeLogsCustomFolderAsync()
        {
            var folder = await storageService.PickFolderAsync().ConfigureAwait(false);
            if (folder == null)
            {
                return;
            }
            Settings.Instance.FileLoggerCustomFolder = await storageService
                .SaveFolderAsync(folder, Settings.Instance.FileLoggerCustomFolder)
                .ConfigureAwait(false);
        }

        private async Task ChangeBackupTaskCustomFolderAsync()
        {
            var folder = await storageService.PickFolderAsync().ConfigureAwait(false);
            if (folder == null)
            {
                return;
            }
            Settings.Instance.AutomatedBackupTaskCustomFolder = await storageService
                .SaveFolderAsync(folder, Settings.Instance.AutomatedBackupTaskCustomFolder)
                .ConfigureAwait(false);
        }
    }
}

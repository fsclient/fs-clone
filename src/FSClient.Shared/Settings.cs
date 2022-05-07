namespace FSClient.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;

    public class Settings : BindableBase, ILogState
    {
        public const string UserSettingsContainer = nameof(UserSettingsContainer);
        public const string StateSettingsContainer = nameof(StateSettingsContainer);
        public const string InternalSettingsContainer = nameof(InternalSettingsContainer);

        public static bool Initialized => instance != null;

        private static Settings? instance;
        public static Settings Instance => instance
            ?? throw new InvalidOperationException($"Static member {nameof(Settings)}.{nameof(Instance)} is null. Don't forget to call {nameof(InitializeSettings)}");

        public static void InitializeSettings(ISettingService settingService)
        {
            if (Initialized)
            {
                throw new InvalidOperationException("Settings was already initialized");
            }
            instance = new Settings(settingService);
        }

        private readonly ISettingService settingService;

        public Settings(ISettingService settings)
        {
            settingService = settings;
        }

        public Settings SetDefault<T>(string settingName, T value)
            where T : unmanaged
        {
            // We don't use SetSetting to not override value in the ISettingService
            // We use GetSetting, so it will respect value from ISettingService at first place
            _ = GetSetting(value, settingName);

            return this;
        }

        #region Shared

        public string? CurrentLanguageISOCode
        {
            get => GetSetting(null);
            set => SetSetting(value);
        }

        public bool LoadAllSources
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool OpenDirectLinkInBrowser
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool AllowDownloadM3U8Files
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool CanPreloadItems
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool CanPreloadFiles
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool CanPreloadEpisodes
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public Site MainSite
        {
            get => Site.Parse(GetSetting(null));
            set => SetSetting(value.Value);
        }

        public Quality PreferredQuality
        {
            get => GetSetting(1080);
            set
            {
                if (!value.IsUnknown)
                {
                    SetSetting((int)value);
                }
            }
        }

        public bool PreferItemSite
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool PositionByEpisode
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool AsyncFolderLoading => false;
        //{
        //    get => GetSetting(false);
        //    set => SetSetting(value);
        //}

        public bool TraceHttp
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool TraceHttpWithCookies
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool TorrServerEnabled
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public string? TorrServerAddress
        {
            get => GetSetting("http://localhost:8090/");
            set => SetSetting(value);
        }

        public bool InternalDatabaseForTorrServer
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool IncludeAdult
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool VNextFeatures
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        #endregion

        #region UWP

        public NodeOpenWay FileOpenWay
        {
            get => (NodeOpenWay)GetSetting((int)NodeOpenWay.InApp);
            set => SetSetting((int)value);
        }

        public ClearCacheModes ClearCacheMode
        {
            get => (ClearCacheModes)GetSetting((int)ClearCacheModes.OnExit);
            set => SetSetting((int)value);
        }

        public bool SerializedDownload
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool OpenNextVideo
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool OpenInFullScreen
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool MoveToVideoPage
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool CompactPlayer
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool PlayerSwipeEnabled
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public NavigationPageType StartPage
        {
            get => (NavigationPageType)GetSetting((int)NavigationPageType.Home);
            set => SetSetting((int)value);
        }

        public bool XYMouseMode
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool ShowImageBackground
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public double VolumeSwipeSensitive
        {
            get => GetSetting(1d);
            set => SetSetting(value);
        }

        public double SeekSwipeSensitive
        {
            get => GetSetting(1d);
            set => SetSetting(value);
        }

        public int SeekForwardStep
        {
            get => GetSetting(30);
            set => SetSetting(value);
        }

        public int SeekBackwardStep
        {
            get => GetSetting(15);
            set => SetSetting(value);
        }

        public bool UseTelemetry
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool InAppLoggerNotifications
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool FileLogger
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public string? FileLoggerCustomFolder
        {
            get => GetSetting(null);
            set => SetSetting(value);
        }

        public bool OpenFavItemsOnFilesPage
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool OpenHistoryItemsOnFilesPage
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool CanShowUpdateChangeLog
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public bool PlayerPauseOnPrimaryTapped
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool PauseOnBackgroundMedia
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool CompactOnFocusLosed
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool MinimalOverlay
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool InvertElapsedPlayerTime
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool ListenForDumpRequest
        {
            // Shouldn't be saved in storage
            get => Get(false);
            set => Set(value);
        }

        public bool HackPlayerControlsVisibility
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool IgnoreLauncherDefaults
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool VerifyAccess
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool PlayerStopButtonEnabled
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public float ApplicationMarginLeft
        {
            get => GetSetting(0f);
            set => SetSetting(value);
        }

        public float ApplicationMarginTop
        {
            get => GetSetting(0f);
            set => SetSetting(value);
        }

        public float ApplicationMarginRight
        {
            get => GetSetting(0f);
            set => SetSetting(value);
        }

        public float ApplicationMarginBottom
        {
            get => GetSetting(0f);
            set => SetSetting(value);
        }

        public bool AutomatedBackupTaskEnabled
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        public string? AutomatedBackupTaskCustomFolder
        {
            get => GetSetting(null);
            set => SetSetting(value);
        }

        public bool ThumbnailFromOnlineVideoSource
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool SeekWithGamepadDPad
        {
            get => GetSetting(false);
            set => SetSetting(value);
        }

        public bool PreferLegacyRemoteLaunchDialog
        {
            get => GetSetting(true);
            set => SetSetting(value);
        }

        #endregion

        private T GetSetting<T>(T otherwise, [CallerMemberName] string settingName = "")
            where T : unmanaged
        {
            return Get(() =>
                settingService.GetSetting(
                    UserSettingsContainer,
                    settingName,
                    otherwise),
                settingName);
        }

        private string? GetSetting(string? otherwise, [CallerMemberName] string settingName = "")
        {
            return Get(() =>
                settingService.GetSetting(
                    UserSettingsContainer,
                    settingName,
                    otherwise),
                settingName);
        }

        private bool SetSetting<T>(T value, [CallerMemberName] string settingName = "")
            where T : unmanaged
        {
            if (Set(value, settingName))
            {
                settingService.SetSetting(UserSettingsContainer, settingName, value);
                return true;
            }
            return false;
        }

        private bool SetSetting(string? value, [CallerMemberName] string settingName = "")
        {
            if (Set(value, settingName))
            {
                settingService.SetSetting(UserSettingsContainer, settingName, value);
                return true;
            }
            return false;
        }

        public IDictionary<string, string> GetLogProperties(bool verbose)
        {
            return new Dictionary<string, string>
            {
                [nameof(LoadAllSources)] = LoadAllSources.ToString(),
                [nameof(PreferItemSite)] = PreferItemSite.ToString(),
                [nameof(MainSite)] = MainSite.Value,
                [nameof(PreferredQuality)] = PreferredQuality.ToString(),
                [nameof(PositionByEpisode)] = PositionByEpisode.ToString(),
                [nameof(TorrServerEnabled)] = TorrServerEnabled.ToString(),
                [nameof(FileOpenWay)] = FileOpenWay.ToString(),
                [nameof(CompactPlayer)] = CompactPlayer.ToString(),
                [nameof(StartPage)] = StartPage.ToString(),
                [nameof(VerifyAccess)] = VerifyAccess.ToString(),
                [nameof(SeekWithGamepadDPad)] = SeekWithGamepadDPad.ToString(),
                [nameof(OpenNextVideo)] = OpenNextVideo.ToString(),
                [nameof(OpenInFullScreen)] = OpenInFullScreen.ToString(),
                [nameof(MoveToVideoPage)] = MoveToVideoPage.ToString()
            };
        }
    }
}

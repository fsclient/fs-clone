namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;

    using Windows.ApplicationModel;
    using Windows.Devices.Input;

    using FSClient.Data.Context;
    using FSClient.Data.Repositories;
    using FSClient.Providers;
    using FSClient.Shared;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Views.Controls;
    using FSClient.UWP.Shared.Views.Dialogs;
    using FSClient.ViewModels;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Debug;

    public sealed class ViewModelLocator : IDisposable
    {
        private static ViewModelLocator? locator;
        private static readonly object lockObject = new object();

        public static bool Initialized => locator != null && locator.provider != null;

        public static ViewModelLocator Current
        {
            get
            {
                if (locator != null)
                {
                    return locator;
                }

                lock (lockObject)
                {
                    return locator ??= new ViewModelLocator(false);
                }
            }
        }

        private readonly IServiceProvider provider;

        public ViewModelLocator(bool isReadOnly = false)
        {
            lock (lockObject)
            {
                locator = this;
            }

            var collection = new ServiceCollection();

            RegisterSettings(collection);

            RegisterLoggers(collection);

            RegisterProviders(collection);

            RegisterServices(collection);

            RegisterRepositories(collection, isReadOnly);

            RegisterManagers(collection);

            RegisterViewModels(collection);

            RegisterIncrementalFactories(collection);

            RegisterDialogs(collection);

            provider = collection.BuildServiceProvider(Debugger.IsAttached);

            Logger.Instance = provider.GetRequiredService<ILogger>();
            ContextSafeEvent.HasAccess = DispatcherHelper.HasAccess;

            provider.GetRequiredService<IProviderManager>()
                .EnsureCurrentMainProvider();
        }

        public IWindowsNavigationService NavigationService =>
            provider.GetRequiredService<IWindowsNavigationService>();

        public TType Resolve<TType>()
            where TType : notnull
        {
            return provider.GetRequiredService<TType>();
        }

        public IContentDialog<TInput, TOuput> ResolveDialog<TInput, TOuput>()
        {
            return provider.GetRequiredService<IContentDialog<TInput, TOuput>>();
        }

        public IContentDialog<TOuput> ResolveDialog<TOuput>()
        {
            return provider.GetRequiredService<IContentDialog<TOuput>>();
        }

        public TViewModel ResolveViewModel<TViewModel>()
            where TViewModel : ViewModelBase
        {
            return provider.GetRequiredService<TViewModel>();
        }

        private static void RegisterViewModels(ServiceCollection collection)
        {
            collection.AddSingleton<DownloadViewModel>();
            collection.AddTransient<FavoriteViewModel>();
            collection.AddTransient<FavoriteMenuViewModel>();
            collection.AddTransient<AuthUserViewModel>();
            collection.AddTransient<HistoryViewModel>();
            collection.AddTransient<HomeViewModel>();
            collection.AddTransient<ItemByTagViewModel>();
            collection.AddTransient<ItemViewModel>();
            collection.AddTransient<MediaViewModel>();
            collection.AddTransient<ReviewViewModel>();
            collection.AddTransient<SearchViewModel>();

            collection.AddSingleton<FileViewModel>();
            collection.AddSingleton<SettingViewModel>();
        }

        private static void RegisterIncrementalFactories(ServiceCollection collection)
        {
            collection.AddTransient(typeof(IIncrementalCollectionFactory),
                typeof(IncrementalLoadingCollectionFactory));
        }

        private static void RegisterDialogs(ServiceCollection collection)
        {
            collection.AddSingleton<IContentDialog<OAuthDialogInput, OAuthDialogOutput>,
                LazyDialog<OAuthDialog, OAuthDialogInput, OAuthDialogOutput>>();

            collection.AddSingleton<IContentDialog<LoginDialogOutput>,
                LazyDialog<LoginDialog, LoginDialogOutput>>();

            collection.AddSingleton<IContentDialog<DeviceFlowDialogInput, AuthStatus>,
                LazyDialog<DeviceFlowDialog, DeviceFlowDialogInput, AuthStatus>>();

            collection.AddSingleton<IContentDialog<string, bool>,
                LazyDialog<ConfirmDialog, string, bool>>();

            collection.AddSingleton<IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput>,
                LazyDialog<BridgeRemoteLaunchDialog, RemoteLaunchDialogInput, RemoteLaunchDialogOutput>>();

            collection.AddSingleton<IContentDialog<BackupDialogInput, BackupDialogOutput>,
                LazyDialog<BackupDialog, BackupDialogInput, BackupDialogOutput>>();

            collection.AddSingleton<IContentDialog<ChangesDialogInput, ChangesDialogOutput>,
                LazyDialog<ChangesDialog, ChangesDialogInput, ChangesDialogOutput>>();
        }

        private static void RegisterSettings(ServiceCollection collection)
        {
            var settingService = new SettingService();
            Settings.InitializeSettings(settingService);

            collection.AddSingleton<ISettingService>(settingService);
            collection.AddSingleton(Settings.Instance);

            var ver = Package.Current.Id.Version;

            Settings.Instance
                .SetDefault(nameof(Settings.PauseOnBackgroundMedia),
                    UWPAppInformation.Instance.DeviceFamily != DeviceFamily.Desktop)
                .SetDefault(nameof(Settings.PlayerPauseOnPrimaryTapped), new TouchCapabilities().TouchPresent == 0)
                .SetDefault(nameof(Settings.MinimalOverlay),
                    UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Mobile);
#if DEV_BUILD
            Settings.Instance
                .SetDefault(nameof(Settings.UseTelemetry), !Debugger.IsAttached)
                .SetDefault(nameof(Settings.TraceHttp), Debugger.IsAttached)
                .SetDefault(nameof(Settings.TraceHttpWithCookies), true)
                .SetDefault(nameof(Settings.InAppLoggerNotifications), Debugger.IsAttached);
#endif
        }

        private static void RegisterManagers(ServiceCollection collection)
        {
            collection.AddSingleton<ICookieManager, CookieManager>();
            collection.AddSingleton<IUserManager, UserManager>();
            collection.AddSingleton<IProviderManager, ProviderManager>();
            collection.AddSingleton<IItemManager, ItemManager>();
            collection.AddSingleton<IFileManager, FileManager>();
            collection.AddSingleton<IFolderManager, FolderManager>();
            collection.AddSingleton<IHistoryManager, HistoryManager>();
            collection.AddSingleton<IDownloadManager, DownloadManager>();
            collection.AddSingleton<IFavoriteManager, FavoriteManager>();
            collection.AddSingleton<IBackupManager, BackupManager>();
            collection.AddSingleton<IPlayerParseManager, PlayerParseManager>();
            collection.AddSingleton<IReviewManager, ReviewManager>();
        }

        private static void RegisterRepositories(ServiceCollection collection, bool isReadOnly)
        {
            collection.AddSingleton(provider => new LiteDatabaseContext(
                provider.GetService<IStorageService>()?.LocalFolder?.Path, isReadOnly));
            collection.AddSingleton<IDatabaseContext>(provider => provider.GetRequiredService<LiteDatabaseContext>());
            if (Settings.Instance.VNextFeatures)
            {
                collection.AddSingleton<IItemInfoRepository, ItemInfoLiteDBRepository>();
                collection.AddSingleton<IHistoryRepository, HistoryLiteDBRepository>();
                collection.AddSingleton<IFavoriteRepository, FavoriteListDBRepository>();
            }
            else
            {
                collection.AddSingleton<IItemInfoRepository, ItemInfoNullRepository>();
                collection.AddSingleton<IHistoryRepository, HistoryJsonRepository>();
                collection.AddSingleton<IFavoriteRepository, LocalSettingFavoriteRepository>();
            }
            collection.AddTransient<ITorrServerRepository, TorrServerRepository>();
            collection.AddTransient<IDownloadRepository, DownloadRepository>();
        }

        private static void RegisterServices(ServiceCollection collection)
        {
            collection.AddTransient<IStoreService, StoreService>();
            collection.AddSingleton<IApplicationService, ApplicationService>();
            collection.AddTransient<ILauncherService, LauncherService>();
            collection.AddTransient<IShareService, ShareService>();
            collection.AddTransient<IStorageService, UWPStorageService>();
            collection.AddTransient<IDownloadService, BackgroundDownloadService>();
            collection.AddSingleton<IAppInformation>(UWPAppInformation.Instance);
            collection.AddSingleton<IWindowsNavigationService, NavigationService>();
            collection.AddSingleton<INavigationService>(provider =>
                provider.GetRequiredService<IWindowsNavigationService>());
            collection.AddTransient<ITorrServerService, TorrServerService>();
            collection.AddTransient<IIPInfoService, MyIPInfoService>();
            collection.AddTransient<IVerificationService, WindowsHelloVerificationService>();
            collection.AddTransient<ITrackRestoreService, TrackRestoreFromSettingService>();
            collection.AddTransient<MarginCalibrationService>();
            collection.AddTransient<BackgroundTaskService>();
            collection.AddTransient<INotificationService, InAppNotificationService>();
            collection.AddSingleton<ITileService, TileService>();
            collection.AddSingleton<PlayerJsParserService>();
            collection.AddTransient<IThirdPartyPlayer, VlcWinRtThirdPartyPlayer>();
            collection.AddSingleton<ICacheService, CacheService>();
            collection.AddSingleton<IProviderConfigService, ProviderConfigService>();
            collection.AddSingleton<IAppLanguageService, UWPAppLanguageService>();
            collection.AddSingleton(provider => provider.GetRequiredService<IApplicationService>()
                .GetApplicationGlobalSettingsFromCache());
            collection.AddTransient<ISystemFeaturesService, SystemFeaturesService>();
        }

        private static void RegisterProviders(ServiceCollection collection)
        {
            var allExportedTypes = typeof(TMDbSiteProvider).GetTypeInfo().Assembly.GetExportedTypes();
            RegisterByInterface<ISiteProvider>();
            RegisterByInterface<IFileProvider>();
            RegisterByInterface<IAuthProvider>();
            RegisterByInterface<IFavoriteProvider>();
            RegisterByInterface<IItemInfoProvider>();
            RegisterByInterface<IItemProvider>();
            RegisterByInterface<ISearchProvider>();
            RegisterByInterface<IReviewProvider>();
            RegisterByInterface<IPlayerParseProvider>();

            void RegisterByInterface<TProvider>()
                where TProvider : IProvider
            {
                var providerInternface = typeof(TProvider);
                var types = allExportedTypes
                    .Where(t => providerInternface.IsAssignableFrom(t) && !t.GetTypeInfo().IsAbstract);
                foreach (var providerType in types)
                {
                    collection.AddSingleton(providerType);
                    collection.AddSingleton(providerInternface, serviceProvider => serviceProvider.GetRequiredService(providerType));
                }
            }
        }

        private static void RegisterLoggers(ServiceCollection collection)
        {
            collection.AddSingleton(provider =>
            {
                var factory = new LoggerFactory();

                factory.AddProvider(new NotificationLogger.Provider(
                    new Lazy<INotificationService>(provider.GetRequiredService<INotificationService>)));

                factory.AddProvider(new AppCenterLogger.Provider());

                factory.AddProvider(new FileLogger.Provider(
                    new Lazy<IAppInformation>(provider.GetRequiredService<IAppInformation>),
                    new Lazy<IStorageService>(provider.GetRequiredService<IStorageService>)));

                if (Debugger.IsAttached)
                {
                    factory.AddProvider(new DebugLoggerProvider());
                }

                factory.AddProvider(new CriticalDialogLogger.Provider());

                return factory.CreateLogger(Package.Current.DisplayName);
            });
        }

        public void Dispose()
        {
            (provider as IDisposable)?.Dispose();
        }
    }
}

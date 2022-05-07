namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Activation;
    using Windows.Foundation.Metadata;
    using Windows.UI.Core.Preview;
    using Windows.UI.Popups;
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Localization.Resources;
    using FSClient.Shared;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Activation;
    using FSClient.UWP.Shared.Extensions;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Views.Controls;
    using FSClient.UWP.Shared.Views.Pages;
    using FSClient.ViewModels;

    using Microsoft.Extensions.Logging;

    public class ActivationService
    {
        private readonly ViewModelLocator locator;
        private bool inited;
        private bool deactivated;

        public ActivationService()
        {
            UWPLoggerHelper.InitGlobalHandlers();

            locator = ViewModelLocator.Current;

            if (UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Xbox)
            {
                UWPAppInformation.Instance.IsXYModeEnabled = Settings.Instance.XYMouseMode;
            }

            ApplyLanguageOnStart();
        }

        private async void ApplyLanguageOnStart()
        {
            await locator.Resolve<IAppLanguageService>().ApplyLanguageAsync(Settings.Instance.CurrentLanguageISOCode);
        }

        public async Task ActivateAsync(Window window, object activationArgs)
        {
            var waitingForActivation = false;
            if (IsInteractive(activationArgs))
            {
                if (!inited)
                {
                    await InitializeAsync(window);
                }

                if (window.Content == null)
                {
                    var mainPage = new MainPage();
                    window.Content = mainPage;

                    waitingForActivation = true;
                    mainPage.Loaded += Element_Loaded;

                    async void Element_Loaded(object _, object __)
                    {
                        mainPage.Loaded -= Element_Loaded;
                        await HandleActivation(activationArgs);
                    }
                }
            }

            if (!waitingForActivation)
            {
                await HandleActivation(activationArgs);
            }

            if (IsInteractive(activationArgs)
                && !inited)
            {
                inited = true;

                window.Activate();
                await VerifyAccessAsync(window);

                await StartupAsync(window);
            }
        }

        private async Task VerifyAccessAsync(Window window)
        {
            if (Settings.Instance.VerifyAccess)
            {
                await ((Page)window.Content).WaitForLoadedAsync();
                var firstGrid = window.Content.FindVisualChild<Grid>()
                                ?? throw new InvalidOperationException("Root Grid is missed.");

                var border = new Border
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = Application.Current.Resources["PaneBackgroundBrush"] as Brush
                };
                firstGrid.Children.Add(border);
                var (success, message) = await locator.Resolve<IVerificationService>().RequestVerificationAsync();
                if (!success)
                {
                    var messageDialog = new MessageDialog(Strings.Activation_NoAccessError, message);
                    await messageDialog.ShowAsync();
                    Application.Current.Exit();
                }

                firstGrid.Children.Remove(border);
            }
        }

        public async Task DeactivateAsync(bool appSuspending)
        {
            if (deactivated)
            {
                return;
            }
            deactivated = true;

            try
            {
                await Task.WhenAll(
                        InvokeTask(Strings.Activation_ClearCache,
                            () => CacheHelper.ClearCacheAsync(false),
                            Settings.Instance.ClearCacheMode.HasFlag(ClearCacheModes.OnExit)),
                        InvokeTask(Strings.Deactivation_StopTorrServerTorrents,
                            () => locator.Resolve<ITorrServerService>().StopActiveTorrentsAsync(CancellationToken.None),
                            Settings.Instance.TorrServerEnabled && appSuspending),
                        InvokeTask(Strings.Activation_DeleteTorrServerTorrents,
                            () => locator.Resolve<ITorrServerService>().StopAndRemoveActiveTorrentsAsync(default),
                            Settings.Instance.TorrServerEnabled && !appSuspending),
                        InvokeTask(Strings.Deactivation_SaveState,
                            () => new SuspendAndResumeHandler(locator.NavigationService,
                                locator.Resolve<ISettingService>()).SaveState().AsTask()))
                    .ConfigureAwait(false);

                await InvokeTask(Strings.Deactivation_DatabaseCheckpoint,
                        () => locator.Resolve<IDatabaseContext>().CheckpointAsync().AsTask())
                    .ConfigureAwait(false);

                if (!appSuspending)
                {
                    locator.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private async Task HandleActivation(object activationArgs)
        {
            try
            {
                var activationHandler = GetActivationHandlers()
                    .FirstOrDefault(h => h.CanHandle(activationArgs));

                if (activationHandler != null)
                {
                    await activationHandler.HandleAsync(activationArgs).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private Task InitializeAsync(Window window)
        {
            try
            {
                var managedWindow = ManagedWindow.GetCurrent(window);
                managedWindow?.SetInitialState();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }

            return Task.CompletedTask;
        }

        private async Task StartupAsync(Window window)
        {
            try
            {
                InitAppSettings(window);

                var initServicesTask = Task.Run(() => Task.WhenAll(
                    InvokeTask(Strings.Activation_ClearCache,
                        () => CacheHelper.ClearCacheAsync(true),
                        Settings.Instance.ClearCacheMode.HasFlag(ClearCacheModes.OnStart)),
                    InvokeTask(Strings.Activation_DeleteTorrServerTorrents,
                        () => locator.Resolve<ITorrServerService>().StopAndRemoveActiveTorrentsAsync(default),
                        Settings.Instance.TorrServerEnabled),
                    InvokeTask(Strings.Activation_UpdateMirrors,
                        UpdateAltMirrorsAndNotify)));

                await Task
                    .WhenAll(
                        initServicesTask,
                        InvokeTask(Strings.Activation_CheckUpdates,
                            () => locator.ResolveViewModel<SettingViewModel>().CheckForUpdatesCommand
                                .ExecuteAsync(false)),
                        InvokeTask(Strings.Activation_CheckSecondaryTiles,
                            () => locator.Resolve<ITileService>().CheckSecondatyTilesAsync(default)),
                        InvokeTask(Strings.Activation_CheckBackgroundTasks,
                            () => locator.Resolve<BackgroundTaskService>().UpdateTaskRegistration()))
                    .ConfigureAwait(true);

                try
                {
                    if (ApiInformation.IsMethodPresent(typeof(SystemNavigationManagerPreview).FullName,
                        nameof(SystemNavigationManagerPreview.GetForCurrentView)))
                    {
                        SystemNavigationManagerPreview.GetForCurrentView().CloseRequested +=
                            ActivationService_CloseRequested;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning(ex);
                }

                locator.Resolve<IHistoryManager>().ItemsHistoryChanged += async (s, a) =>
                {
                    var tileService = locator.Resolve<ITileService>();
                    if (tileService != null)
                    {
                        await tileService.UpdateTimelineAsync(a.Items, a.Reason == HistoryItemChangedReason.Removed, default).ConfigureAwait(false);
                        if (a.Reason != HistoryItemChangedReason.Update)
                        {
                            var allItems = locator.Resolve<IHistoryManager>().GetItemsHistory();
                            await tileService.SetRecentItemsToJumpListAsync(allItems, default).ConfigureAwait(false);
                        }
                    }
                };

                await InvokeTask(Strings.Activation_EnsureDisplayMargin,
                        () => locator.Resolve<MarginCalibrationService>()
                            .EnsureMarginCalibratedAsync(false, CancellationToken.None))
                    .ConfigureAwait(false);

                SendInformationLog();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
                await locator.Resolve<INotificationService>().ShowAsync(
                    Strings.Activation_ComponentError,
                    NotificationType.Error).ConfigureAwait(false);
            }
        }

        private async Task InvokeTask(string taskName, Func<Task> task, bool condition = true)
        {
            if (!condition)
            {
                return;
            }

            try
            {
                await task().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ex.Data["TaskName"] = taskName;
                Logger.Instance.LogError(ex);

                _ = locator.Resolve<INotificationService>().ShowAsync(
                    $"{Strings.Activation_ComponentError}:{Environment.NewLine}{taskName}",
                    NotificationType.Error);
            }

            ;
        }

        private void SendInformationLog()
        {
            try
            {
                var distributionType = locator.Resolve<IStoreService>().DistributionType;
                var appInformation = locator.Resolve<IAppInformation>();
                var appLanguageService = locator.Resolve<IAppLanguageService>();
                var state = appInformation.GetLogProperties(false);
                state[nameof(IStoreService.DistributionType)] = distributionType.ToString();
                state["CurrentLanguage"] = appLanguageService.GetCurrentLanguage();

                Logger.Instance.Log(LogLevel.Information, default, state, null, (_, __) => "ApplicationOpened");

                Logger.Instance.Log(LogLevel.Information, default, Settings.Instance, null, (_, __) => "TrackSettings");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private async Task UpdateAltMirrorsAndNotify()
        {
            var updated = await ViewModelLocator.Current.Resolve<IApplicationService>()
                .LoadApplicationGlobalSettingsToCacheAsync(default)
                .ConfigureAwait(false);
            if (updated)
            {
                await locator.Resolve<INotificationService>()
                    .ShowAsync(Strings.Activation_MirrorsWasUpdated, NotificationType.Information)
                    .ConfigureAwait(false);
            }
        }

        private static void InitAppSettings(Window window)
        {
            if (Settings.Instance.ClearCacheMode.HasFlag(ClearCacheModes.OnTimer))
            {
                CacheHelper.StartAutoClean();
            }

            if (UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Desktop)
            {
                CompatExtension.SetXYFocusKeyboardNavigation(window.Content, true);
            }

            Settings.Instance.PropertyChanged += (s, a) =>
            {
                if (a.PropertyName == nameof(Settings.Instance.ClearCacheMode))
                {
                    if (Settings.Instance.ClearCacheMode.HasFlag(ClearCacheModes.OnTimer))
                    {
                        CacheHelper.StartAutoClean();
                    }
                    else
                    {
                        CacheHelper.StopAutoClean();
                    }
                }
            };
        }

        private IEnumerable<IActivationHandler> GetActivationHandlers()
        {
            yield return new ShareTargetActivationHandler(locator.Resolve<ILauncherService>());
            yield return new SearchActivationHandler(locator.NavigationService);
            yield return new SuspendAndResumeHandler(locator.NavigationService, locator.Resolve<ISettingService>());
            yield return new SchemeActivationHandler(locator.NavigationService, locator.Resolve<IHistoryManager>());
            yield return new DefaultLaunchActivationHandler(locator.NavigationService);
        }

        private static bool IsInteractive(object args)
        {
#if WINUI3
            if (args is Microsoft.UI.Xaml.LaunchActivatedEventArgs)
            {
                return true;
            }
#endif
            return args is IActivatedEventArgs && !(args is ShareTargetActivatedEventArgs);
        }

        private async void ActivationService_CloseRequested(object? sender,
            SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            var deferral = e.GetDeferral();
            try
            {
                await DeactivateAsync(appSuspending: false).ConfigureAwait(true);
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}

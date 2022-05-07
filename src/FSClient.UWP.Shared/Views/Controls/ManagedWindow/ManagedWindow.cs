namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Core;
    using Windows.Foundation;
    using Windows.Foundation.Metadata;
    using Windows.UI.Core;
    using Windows.UI.ViewManagement;

#if !UNO && WINUI3
    using CoreWindowActivationState = Microsoft.UI.Xaml.WindowActivationState;
    using VisibilityChangedEventArgs = Microsoft.UI.Xaml.WindowVisibilityChangedEventArgs;
#endif
#if WINUI3
    using Microsoft.UI.Xaml;
    using WindowActivatedEventArgs = Microsoft.UI.Xaml.WindowActivatedEventArgs;
#else
    using System.Collections.Generic;
    using System.Threading;

    using Windows.UI.Xaml;

    using TreeDumpLibrary;

    using Windows.System;
    using Windows.Storage;
    using Windows.Storage.Pickers;

    using FSClient.Localization.Resources;
    using FSClient.UWP.Shared.Views.Dialogs;
#endif

    using FSClient.Shared;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;

    using Nito.AsyncEx;

    public partial class ManagedWindow
    {
        private bool isInited;
        private Size? overlaySize;
        private WeakReference<Window>? windowWeakRef;
        private ApplicationViewBoundsMode? previousViewBoundsMode;

        private readonly bool focusVisualKindAvailable = ApiInformation
            .IsMethodPresent(typeof(Application).FullName, nameof(Application.FocusVisualKind));

        private readonly bool focusVisualKindRevealAvailable = ApiInformation
            .IsEnumNamedValuePresent(typeof(FocusVisualKind).FullName, nameof(FocusVisualKind.Reveal));

        private readonly ISettingService settingService;

        private ManagedWindow()
        {
            settingService = ViewModelLocator.Current.Resolve<ISettingService>();
        }

        public void SetInitialState()
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                var fth = new FocusTrackerHelper();
                fth.BindToTitleBar();
                fth.IsActive = true;
                ApplicationView?.SetPreferredMinSize(new Size(192, 48));
            }
#endif

            if (focusVisualKindAvailable
                && focusVisualKindRevealAvailable)
            {
                Application.Current.FocusVisualKind = FocusVisualKind.Reveal;
            }

            (Application.Current.Resources["AccentColor"] as AccentColor)?.Setup();
            if (CoreApplication.GetCurrentView().TitleBar is CoreApplicationViewTitleBar titleBar)
            {
                titleBar.ExtendViewIntoTitleBar = false;
            }
        }

        public async Task<bool> ShowAsync(object? parameter = null)
        {
            if (!isInited
                && !await InitializeAsync().ConfigureAwait(true))
            {
                return false;
            }

            IsActive = await CurrentWindowDispather!
                .CheckBeginInvokeOnUI(async () =>
                {
                    try
                    {
                        return await ApplicationViewSwitcher
                            .TryShowAsStandaloneAsync(ApplicationView!.Id, ViewSizePreference.Custom);
                    }
                    catch (Exception ex)
                        when (unchecked((uint)ex.HResult) == 0x87B20C16)
                    {
                        // Ignore unknown winrt error
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogWarning(ex);
                    }

                    return false;
                })
                .ConfigureAwait(true);

            if (IsActive)
            {
                Showed?.Invoke(this, new WindowShowedEventArgs(parameter, false));
            }

            return IsActive;
        }

        public async Task<bool> ShowOverlayAsync(object? parameter = null)
        {
            if (!OverlaySupported)
            {
                return false;
            }

            if (!isInited
                && !await InitializeAsync().ConfigureAwait(true))
            {
                return false;
            }

            if (IsActive)
            {
                return await SetWindowModeAsync(WindowMode.CompactOverlay).ConfigureAwait(true);
            }

            var result = IsActive = await CurrentWindowDispather!
                .CheckBeginInvokeOnUI(() =>
                {
                    try
                    {
                        return ApplicationViewSwitcher
                            .TryShowAsViewModeAsync(ApplicationView!.Id, ApplicationViewMode.CompactOverlay)
                            .AsTask();
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogWarning(ex);
                        return Task.FromResult(false);
                    }
                })
                .ConfigureAwait(true);

            if (result)
            {
                if (OverlaySize.HasValue)
                {
                    var coreApplicationView = CoreApplication.GetCurrentView();
                    await coreApplicationView.Dispatcher.CheckBeginInvokeOnUI(() =>
                    {
                        try
                        {
                            coreApplicationView.TitleBar.ExtendViewIntoTitleBar = true;
                            ApplicationView!.TryResizeView(OverlaySize.Value);
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.LogWarning(ex);
                        }
                    });
                }

                Showed?.Invoke(this, new WindowShowedEventArgs(parameter, true));
            }

            return result;
        }

        public Task<bool> CloseAsync()
        {
            if (!isInited)
            {
                return Task.FromResult(false);
            }

            return CoreApplication.GetCurrentView().Dispatcher.CheckBeginInvokeOnUI(
                () => ApplicationView!.TryConsolidateAsync().AsTask(),
                CoreDispatcherPriority.High);
        }

        public Task<bool> SetWindowModeAsync(WindowMode windowMode)
        {
            if (!isInited)
            {
                return Task.FromResult(false);
            }

            if (WindowMode == windowMode)
            {
                return Task.FromResult(true);
            }

            return CoreApplication.GetCurrentView().Dispatcher.CheckBeginInvokeOnUI(() =>
            {
                try
                {
                    switch (windowMode)
                    {
                        case WindowMode.FullScreen:
                            var result = ApplicationView!.TryEnterFullScreenMode();
                            return Task.FromResult(result);
                        case WindowMode.CompactOverlay
                            when OverlaySize.HasValue && OverlaySupported:
                            var preferences = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);
                            preferences.ViewSizePreference = ViewSizePreference.Custom;
                            preferences.CustomSize = OverlaySize.Value;
                            return ApplicationView!
                                .TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay, preferences).AsTask();
                        case WindowMode.CompactOverlay
                            when OverlaySupported:
                            return ApplicationView!.TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay).AsTask();
                        case WindowMode.None:
                            ApplicationView!.ExitFullScreenMode();
                            return OverlaySupported
                                ? ApplicationView.TryEnterViewModeAsync(ApplicationViewMode.Default).AsTask()
                                : Task.FromResult(true);
                        default:
                            throw new NotSupportedException($"WindowMode.{windowMode} is not supported");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning(ex);
                    return Task.FromResult(false);
                }
            }, CoreDispatcherPriority.High);
        }

        protected void SetContent(UIElement? content)
        {
            if (windowWeakRef == null
                || !windowWeakRef.TryGetTarget(out var window))
            {
                throw new InvalidOperationException("Window is not initialized");
            }

            window.Content = content;
        }

        private Task<bool> InitializeAsync()
        {
            if (isInited)
            {
                throw new InvalidOperationException("Already initialized");
            }

            var coreApplicationView = CoreApplication.CreateNewView();

            return coreApplicationView.Dispatcher.CheckBeginInvokeOnUI(async () =>
            {
                try
                {
                    var window = Window.Current
#if WINUI3 && !UNO
                        ?? new Window();
#endif
                        ?? throw new InvalidOperationException("Window cannot be created");
                    windowWeakRef = new WeakReference<Window>(window);

                    InitCurrentAppViewAndWindow();
                    Settings.Instance.PropertyChanged += Instance_PropertyChanged;

                    SetInitialState();

                    if (Initing != null)
                    {
                        var deferralManager = new DeferralManager();
                        Initing.Invoke(this, deferralManager.DeferralSource);
                        await deferralManager.WaitForDeferralsAsync().ConfigureAwait(true);
                    }

                    window.Activate();

                    isInited = true;

                    windows.TryAdd(ApplicationView!.Id, this);

                    Initialized?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    isInited = false;
                    Logger.Instance.LogWarning(ex);
                    await DestroyAsync().ConfigureAwait(true);
                }

                return isInited;
            });
        }

        private bool InitializeFromCurrent(Window window)
        {
            if (isInited)
            {
                throw new InvalidOperationException("Already initialized");
            }
            windowWeakRef = new WeakReference<Window>(window);

            try
            {
                InitCurrentAppViewAndWindow();
                Settings.Instance.PropertyChanged += Instance_PropertyChanged;

                IsActive = window.Visible;
                isInited = true;
            }
            catch (Exception ex)
            {
                UnsubscribeFromEvents(window);
                isInited = false;
                Logger.Instance.LogError(ex);
            }

            return isInited;
        }

        private void InitCurrentAppViewAndWindow()
        {
            if (windowWeakRef == null
                || !windowWeakRef.TryGetTarget(out var window))
            {
                throw new InvalidOperationException("Window is not initialized");
            }

            ApplicationView = ApplicationView.GetForCurrentView();
            ApplicationView.FullScreenSystemOverlayMode = Settings.Instance.MinimalOverlay
                ? FullScreenSystemOverlayMode.Minimal
                : FullScreenSystemOverlayMode.Standard;

            if (UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Xbox)
            {
                ApplicationView.SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
            }

            ApplicationView.Consolidated += AppView_Consolidated;
            ApplicationView.VisibleBoundsChanged += AppView_VisibleBoundsChanged;

            if (Settings.Instance.ListenForDumpRequest)
            {
                CoreApplication.GetCurrentView().CoreWindow.KeyDown += CoreWindow_KeyDown;
            }

            window.Activated += Current_Activated;
            window.VisibilityChanged += Current_VisibilityChanged;
        }

        private async void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            switch (args.VirtualKey)
            {
#if UAP
                case VirtualKey.D
                when sender.GetKeyState(VirtualKey.Space).HasFlag(CoreVirtualKeyStates.Down):
                    var result = await new LazyDialog<ConfirmDialog, string, bool>()
                       .ShowAsync(
                           Strings.ManagedWindow_DumpVisualTreeConfirmation,
                           CancellationToken.None)
                       .ConfigureAwait(true);
                    if (result)
                    {
                        if (windowWeakRef == null
                            || !windowWeakRef.TryGetTarget(out var window))
                        {
                            throw new InvalidOperationException("Window is not initialized");
                        }

                        var rootElement = (FrameworkElement?)window.Content;
                        var next = rootElement;
                        while (next != null)
                        {
                            rootElement = next;
                            next = rootElement.FindVisualAscendant<FrameworkElement>();
                        }
                        if (rootElement != null)
                        {
                            var stringDump = VisualTreeDumper.DumpTree(rootElement, null, new List<string>(), new List<AttachedProperty>());

                            var savePicker = new FileSavePicker();
                            savePicker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
                            savePicker.SuggestedFileName = "fsclient_visual_dump.json";

                            var file = await savePicker.PickSaveFileAsync();
                            if (file != null)
                            {
                                await FileIO.WriteTextAsync(file, stringDump);
                            }
                        }
                    }
                    break;
#endif
            }
        }

        private void Instance_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.MinimalOverlay))
            {
                ApplicationView!.FullScreenSystemOverlayMode = Settings.Instance.MinimalOverlay
                    ? FullScreenSystemOverlayMode.Minimal
                    : FullScreenSystemOverlayMode.Standard;
            }
            else if (e.PropertyName == nameof(Settings.ListenForDumpRequest)
                     && CoreApplication.GetCurrentView().CoreWindow is CoreWindow coreWindow)
            {
                coreWindow.KeyDown -= CoreWindow_KeyDown;
                if (Settings.Instance.ListenForDumpRequest)
                {
                    coreWindow.KeyDown += CoreWindow_KeyDown;
                }
            }
        }

        private void AppView_VisibleBoundsChanged(ApplicationView sender, object args)
        {
            if (IsActive
                && OverlaySupported
                && sender.ViewMode == ApplicationViewMode.CompactOverlay
                && !sender.IsFullScreenMode)
            {
                OverlaySize = new Size(sender.VisibleBounds.Width, sender.VisibleBounds.Height);
            }

            var oldWindowMode = WindowMode;
            if (sender.IsFullScreenMode)
            {
                WindowMode = WindowMode.FullScreen;
                if (oldWindowMode != WindowMode.FullScreen)
                {
                    previousViewBoundsMode = sender.DesiredBoundsMode;
                    sender.SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
                }
            }
            else if (OverlaySupported
                     && sender.ViewMode == ApplicationViewMode.CompactOverlay)
            {
                WindowMode = WindowMode.CompactOverlay;
                if (oldWindowMode != WindowMode.CompactOverlay)
                {
                    if (CoreApplication.GetCurrentView().TitleBar is CoreApplicationViewTitleBar titleBar)
                    {
                        titleBar.ExtendViewIntoTitleBar = true;
                    }

                    if (previousViewBoundsMode.HasValue)
                    {
                        sender.SetDesiredBoundsMode(previousViewBoundsMode.Value);
                        previousViewBoundsMode = null;
                    }
                }
            }
            else if (oldWindowMode != WindowMode.None)
            {
                WindowMode = WindowMode.None;
                if (CoreApplication.GetCurrentView().TitleBar is CoreApplicationViewTitleBar titleBar)
                {
                    titleBar.ExtendViewIntoTitleBar = false;
                }

                if (previousViewBoundsMode.HasValue)
                {
                    sender.SetDesiredBoundsMode(previousViewBoundsMode.Value);
                    previousViewBoundsMode = null;
                }
            }

            if (oldWindowMode != WindowMode)
            {
                WindowModeChanged?.Invoke(this, new WindowModeChangedEventArgs(WindowMode));
            }
        }

        private async void AppView_Consolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            await DestroyAsync(alreadyClosing: true).ConfigureAwait(true);
        }

        private void UnsubscribeFromEvents(Window? window)
        {
            if (window != null)
            {
                window.Activated -= Current_Activated;
                window.VisibilityChanged -= Current_VisibilityChanged;
            }

            Settings.Instance.PropertyChanged -= Instance_PropertyChanged;
            if (ApplicationView != null)
            {
                ApplicationView.VisibleBoundsChanged -= AppView_VisibleBoundsChanged;
                ApplicationView.Consolidated -= AppView_Consolidated;
            }

            if (CoreApplication.GetCurrentView().CoreWindow is CoreWindow coreWindow)
            {
                coreWindow.KeyDown -= CoreWindow_KeyDown;
            }
        }

        private async Task DestroyAsync(bool alreadyClosing = false)
        {
            var id = 0;
            try
            {
                id = ApplicationView?.Id ?? 0;
                var wasMain = CoreApplication.GetCurrentView().IsMain;
                var window = Window.Current;

                UnsubscribeFromEvents(window);

                if (Destroying is EventHandler<IDeferralSource> eventHandler)
                {
                    var deferralManager = new DeferralManager();
                    eventHandler.Invoke(this, deferralManager.DeferralSource);
                    await deferralManager.WaitForDeferralsAsync().ConfigureAwait(true);
                }

                ApplicationView = null;

                if (wasMain)
                {
                    Application.Current.Exit();
                }
                else if (!alreadyClosing)
                {
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }
            finally
            {
                IsActive = isInited = false;
                windows.TryRemove(id, out _);
                Destroyed?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Current_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (!IsActive)
            {
                IsActive = e.Visible;
            }

            FocusChanged?.Invoke(this, new FocusChangedEventArgs(e.Visible, e.Visible));
        }

        private void Current_Activated(object sender, WindowActivatedEventArgs e)
        {
            var activeted = e.WindowActivationState != CoreWindowActivationState.Deactivated;
            if (!IsActive)
            {
                IsActive = activeted;
            }

            FocusChanged?.Invoke(this, new FocusChangedEventArgs(IsActive, activeted));
        }
    }
}

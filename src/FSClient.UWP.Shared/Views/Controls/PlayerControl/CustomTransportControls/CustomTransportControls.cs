namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Windows.Devices.Input;
    using Windows.Foundation;
    using Windows.Foundation.Metadata;
    using Windows.Media.Playback;
    using Windows.UI.Core;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Documents;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Documents;
#endif

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.UWP.Shared.Helpers;

    public partial class CustomTransportControls : MediaTransportControls
    {
        private static readonly bool IsGeneratedAvaiable =
            ApiInformation.IsPropertyPresent(typeof(PointerRoutedEventArgs).FullName,
                nameof(PointerRoutedEventArgs.IsGenerated));

        private const double seekSwipeRatio = 1.6;
        private const double volumeSwipeRatio = 0.0015;
        private const double wheelRatio = 5d / 12000;
        private const int hideIntervalSeconds = 4;

        private readonly DispatcherTimer hideViewTimer;
        private readonly DispatcherTimer hideInfoPanelTimer;
        private readonly DispatcherTimer togglePauseTimer;
        private readonly TouchCapabilities touchCapabilities;

        private readonly WeakReference<PlayerControl> playerControlReference;
        private Grid? rootGrid;
        private TextBlock? infoTextBlock;
        private Border? infoBorder;
        private SplitView? playlistSplitView;
        private MediaSettings? mediaSettings;
        private MediaHeader? mediaHeader;
        private ListView? mediaPlaylist;
        private MediaMainControls? mediaMainControls;
        private Flyout? volumeFlyout;
        private Flyout? settingsFlyout;
        private CoreCursor? tempCursor;

        private Point? startSwipePoint;
        private double? startSwipeVolume;
        private double? lastSwipeVolumeDelta;
        private GestureActionType currentGestureActionType;
        private bool isPlayingOrPaused;
        private ControlAnimationState controlAnimationState;
        private TimeSpan lastDisplayedSeek;
        private TimeSpan? lastSwipeSeekDelta;
        private Video? lastPlayingVideo;

        public CustomTransportControls(PlayerControl player)
        {
            playerControlReference = new WeakReference<PlayerControl>(player);
            DataContext = this;

            hideViewTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(hideIntervalSeconds)};

            hideInfoPanelTimer = new DispatcherTimer();

            togglePauseTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(200)};

            lastDisplayedSeek = TimeSpan.Zero;

            DefaultStyleKey = nameof(CustomTransportControls);

            Background = new SolidColorBrush {Opacity = 0};

            touchCapabilities = new TouchCapabilities();

            Loaded += CustomTransportControls_Loaded;
            Unloaded += CustomTransportControls_Unloaded;
        }

        private static string InStoryboardState => "InStoryboardState";

        private static string OutStoryboardState => Settings.Instance.HackPlayerControlsVisibility
            ? "HackOutStoryboardState"
            : "OutStoryboardState";

        public void ToggleView()
        {
            if (controlAnimationState == ControlAnimationState.Shown
                || controlAnimationState == ControlAnimationState.Showing)
            {
                controlAnimationState = ControlAnimationState.Hidding;
                VisualStateManager.GoToState(this, OutStoryboardState, true);
            }
            else
            {
                controlAnimationState = ControlAnimationState.Showing;
                VisualStateManager.GoToState(this, InStoryboardState, true);
                if (isPlayingOrPaused)
                {
                    hideViewTimer.Start();
                }
            }
        }

        public void ShowView()
        {
            if (controlAnimationState == ControlAnimationState.Hidding
                || controlAnimationState == ControlAnimationState.Hidden)
            {
                controlAnimationState = ControlAnimationState.Showing;
                VisualStateManager.GoToState(this, InStoryboardState, true);
                ShowCursor();
            }

            if (isPlayingOrPaused)
            {
                hideViewTimer.Start();
            }
        }

        public void HideView()
        {
            if (controlAnimationState == ControlAnimationState.Hidding
                || controlAnimationState == ControlAnimationState.Hidden)
            {
                return;
            }

            controlAnimationState = ControlAnimationState.Hidding;
            VisualStateManager.GoToState(this, OutStoryboardState, true);

            if (hideViewTimer.IsEnabled)
            {
                hideViewTimer.Stop();
            }

            if (hideInfoPanelTimer.IsEnabled)
            {
                HideInfoPanel();
            }
        }

        public bool HidePlaylist()
        {
            if (playlistSplitView?.IsPaneOpen == true)
            {
                playlistSplitView.IsPaneOpen = false;
                return true;
            }

            return false;
        }

        public void ShowInfoPanel(TimeSpan? duration = null)
        {
            if (infoBorder == null)
            {
                return;
            }

            infoBorder.Visibility = Visibility.Visible;

            ShowView();

            hideInfoPanelTimer.Interval = duration ?? TimeSpan.FromSeconds(1);
            hideInfoPanelTimer.Start();
        }

        public void HideInfoPanel()
        {
            hideInfoPanelTimer.Stop();
            infoTextBlock!.Text = "";
            infoBorder!.Visibility = Visibility.Collapsed;
            lastDisplayedSeek = TimeSpan.Zero;
        }

        protected override void OnApplyTemplate()
        {
            if (playerControlReference.TryGetTarget(out var playerControl))
            {
                InitPlayerEventHandlers(playerControl);

                InitHeader(playerControl);

                InitSettingsFlyout();

                InitMainControls();

                InitRootGrid(playerControl);

                InitPlaylist();

                InitStoryboards();
            }

            base.OnApplyTemplate();
        }

        private void InitPlaylist()
        {
            mediaPlaylist = GetTemplateChild("MediaPlaylist") as ListView
                            ?? throw new InvalidOperationException("MediaPlaylist element is missed");
            mediaPlaylist.SelectionChanged += async (s, _) =>
            {
                if (playerControlReference.TryGetTarget(out var playerControl)
                    && playerControl.SetAndPreloadFileCommand != null
                    && mediaPlaylist.SelectedItem is File file)
                {
                    await playerControl.SetAndPreloadFileCommand.ExecuteAsync(file);
                }
            };

            if (lastPlayingVideo?.ParentFile is File file)
            {
                mediaPlaylist.ItemsSource = file.Playlist;
                mediaPlaylist.SelectedItem = file;
            }
        }

        private void InitHeader(PlayerControl playerControl)
        {
            mediaHeader = GetTemplateChild("MediaHeader") as MediaHeader
                          ?? throw new InvalidOperationException("MediaHeader element is missed");

            mediaHeader.CurrentFile = lastPlayingVideo?.ParentFile;
            mediaHeader.GoNextCommand = playerControl.GoNextCommand;
            mediaHeader.GoPreviousCommand = playerControl.GoPreviousCommand;
        }

        private async void InitMainControls()
        {
            mediaMainControls = GetTemplateChild("MediaMainControls") as MediaMainControls
                                ?? throw new InvalidOperationException("MediaMainControls element is missed");

            mediaMainControls.CastToRequested += (s, _) =>
            {
                if (playerControlReference.TryGetTarget(out var playerControl))
                {
                    playerControl.OpenCastToDialog();
                }
            };
            mediaMainControls.StopRequested += async (s, _) =>
            {
                if (playerControlReference.TryGetTarget(out var playerControl))
                {
                    await playerControl.StopAsync().ConfigureAwait(false);
                }
            };
            mediaMainControls.PlayPauseToggleRequested += async (s, _) =>
            {
                if (playerControlReference.TryGetTarget(out var playerControl))
                {
                    await playerControl.TogglePlayPauseAsync().ConfigureAwait(false);
                }
            };
            mediaMainControls.WindowModeToggleRequested += (s, arg) =>
            {
                if (playerControlReference.TryGetTarget(out var playerControl))
                {
                    if (arg.Argument == WindowMode.FullScreen)
                    {
                        playerControl.ToggleFullscreen();
                    }
                    else if (arg.Argument == WindowMode.CompactOverlay)
                    {
                        playerControl.WindowMode = playerControl.WindowMode == WindowMode.CompactOverlay
                            ? WindowMode.None
                            : WindowMode.CompactOverlay;
                    }
                }
            };
            mediaMainControls.SeekRequested += (s, arg) =>
            {
                if (playerControlReference.TryGetTarget(out var playerControl))
                {
                    playerControl.Seek(arg.Argument, PositionChangeType.Keyboard);
                }
            };
            mediaMainControls.PositionChangeRequested += (s, arg) =>
            {
                if (playerControlReference.TryGetTarget(out var playerControl))
                {
                    playerControl.Position = arg.Argument;
                }
            };
            mediaMainControls.RewindRequested += (s, _) =>
            {
                if (playerControlReference.TryGetTarget(out var playerControl))
                {
                    playerControl.Rewind(SeekModifier.Auto, PositionChangeType.PlayerControl);
                }
            };
            mediaMainControls.FastForwardRequested += (s, _) =>
            {
                if (playerControlReference.TryGetTarget(out var playerControl))
                {
                    playerControl.FastForward(SeekModifier.Auto, PositionChangeType.PlayerControl);
                }
            };

            await Dispatcher.YieldIdle();

            if (mediaMainControls.FindVisualChild<MediaSlider>() is MediaSlider mediaSlider)
            {
                mediaSlider.ThumbnailRequested += async (slider, args) =>
                {
                    if (playerControlReference.TryGetTarget(out var playerControl))
                    {
                        using var _ = args.DeferralSource.GetDeferral();
                        args.ThumbnailImage = await playerControl
                            .GrabFrameStreamAsync(args.Position, args.Size, args.CancellationToken)
                            .ConfigureAwait(true);
                    }
                };
            }
        }

        private void InitPlayerEventHandlers(PlayerControl playerControl)
        {
            playerControl.VideoOpening += VideoOpening;

            playerControl.VideoOpened += VideoOpened;

            playerControl.VolumeChanged += VolumeChanged;

            playerControl.PositionChanged += PositionChanged;

            playerControl.StateChanged += StateChanged;

            playerControl.WindowModeChanged += WindowModeChanged;

            playerControl.BufferingChanged += BufferingChanged;

            playerControl.BufferedRangesChanged += BufferedRangesChanged;

            playerControl.PlaybackRateChanged += PlaybackRateChanged;
        }

        private void InitRootGrid(PlayerControl playerControl)
        {
            infoTextBlock = GetTemplateChild("InfoTextBlock") as TextBlock;
            infoBorder = GetTemplateChild("InfoBorder") as Border;
            playlistSplitView = GetTemplateChild("PlaylistSplitView") as SplitView;

            rootGrid = GetTemplateChild("RootGrid") as Grid
                       ?? throw new InvalidOperationException("RootGrid element is missed");

            if (infoBorder != null
                && infoTextBlock != null)
            {
                WindowModeChanged(playerControl, new WindowModeChangedEventArgs(playerControl.WindowMode));

                rootGrid.Tapped += RootGrid_Tapped;
                rootGrid.RightTapped += RootGrid_RightTapped;
                rootGrid.PointerMoved += RootGrid_PointerMoved;
                rootGrid.PointerPressed += TogglePauseOnMiddleClick;
                rootGrid.DoubleTapped += ToggleFullScreenOnDoubleTap;
                rootGrid.PointerWheelChanged += WheelChanged;
                rootGrid.ManipulationStarted += SwipeStart;
                rootGrid.ManipulationDelta += SwipeDelta;
                rootGrid.ManipulationCompleted += SwipeCompleted;

                if (IsCompact)
                {
                    rootGrid.SizeChanged += RootGrid_SizeChanged;
                }
            }

            var groups = VisualStateManager.GetVisualStateGroups(rootGrid);
            groups.First(g => g.Name == "MediaTransportControlMode")
                .CurrentStateChanged += Relay_CurrentStateChanged;
            groups.First(g => g.Name == "VolumeMuteStates")
                .CurrentStateChanged += Relay_CurrentStateChanged;
            groups.First(g => g.Name == "MediaStates")
                .CurrentStateChanged += Relay_CurrentStateChanged;

            void Relay_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
            {
                VisualStateManager.GoToState(mediaMainControls, e.NewState.Name, false);
                VisualStateManager.GoToState(mediaHeader, e.NewState.Name, false);
            }
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var grid = (Grid)sender;
            if (e.NewSize.Width > 680)
            {
                grid.Padding = e.NewSize.Width > 1080
                    ? new Thickness(e.NewSize.Width * (32f / 1080))
                    : grid.Padding = new Thickness(32);
            }
            else
            {
                grid.Padding = new Thickness(0);
            }
        }

        private void InitStoryboards()
        {
            volumeFlyout = GetTemplateChild("VolumeFlyout") as Flyout;
            settingsFlyout = GetTemplateChild("SettingsFlyout") as Flyout;

            var states = VisualStateManager.GetVisualStateGroups(rootGrid)?.FirstOrDefault()?.States ??
                         new List<VisualState>();

            var showStoryboard = states.FirstOrDefault(state => state.Name == InStoryboardState)?.Storyboard;
            var hideStoryboard = states.FirstOrDefault(state => state.Name == OutStoryboardState)?.Storyboard;

            if (showStoryboard != null)
            {
                showStoryboard.Completed += ShowStoryboard_Completed;
            }

            if (hideStoryboard != null)
            {
                hideStoryboard.Completed += HideStoryboard_Completed;
            }
        }

        private void HideStoryboard_Completed(object? sender, object e)
        {
            controlAnimationState = ControlAnimationState.Hidden;
            volumeFlyout?.Hide();
            settingsFlyout?.Hide();
            HideCursor();
        }

        private void ShowStoryboard_Completed(object? sender, object e)
        {
            controlAnimationState = ControlAnimationState.Shown;
            ShowCursor();
        }

        private void InitSettingsFlyout()
        {
            mediaSettings = GetTemplateChild("MediaSettings") as MediaSettings
                            ?? throw new InvalidOperationException("MediaSettings element is missed");

            mediaSettings.Loaded += MediaSettings_Loaded;

            void MediaSettings_Loaded(object sender, RoutedEventArgs e)
            {
                if (playerControlReference.TryGetTarget(out var playerControl))
                {
                    mediaSettings!.PlaybackRate = playerControl.PlaybackRate;
                    mediaSettings.CurrentStretch = playerControl.Stretch;
                }
                else
                {
                    return;
                }

                mediaSettings.PlaybackRateChangeRequested += (_, a) =>
                {
                    if (CanHandleEvent(out var playerControl))
                    {
                        playerControl.PlaybackRate = a.Argument;
                    }
                };

                mediaSettings.StretchChangeRequested += (_, a) =>
                {
                    if (CanHandleEvent(out var playerControl))
                    {
                        playerControl.Stretch = a.Argument;
                    }
                };

                mediaSettings.SubtitleTrackChangeRequested += (_, a) =>
                {
                    if (CanHandleEvent(out var playerControl))
                    {
                        playerControl.CurrentSubtitleTrack = a.Argument;
                    }
                };

                mediaSettings.AudioTrackChangeRequested += (_, a) =>
                {
                    if (CanHandleEvent(out var playerControl))
                    {
                        playerControl.CurrentAudioTrack = a.Argument;
                    }
                };

                mediaSettings.VideoChangeRequested += (_, a) =>
                {
                    if (CanHandleEvent(out var playerControl))
                    {
                        playerControl.CurrentVideo = a.Argument;
                    }
                };
            }

            bool CanHandleEvent(out PlayerControl playerControl)
            {
                return playerControlReference.TryGetTarget(out playerControl)
                       && lastPlayingVideo != null
                       && lastPlayingVideo == playerControl.PlayingVideo;
            }
        }

        private void CustomTransportControls_Unloaded(object sender, RoutedEventArgs e)
        {
            hideViewTimer.Tick -= HideViewTimer_Tick;
            hideInfoPanelTimer.Tick -= HideInfoPanelTimer_Tick;
            togglePauseTimer.Tick -= TogglePauseTimer_Tick;

            var coreWindow = CoreWindow.GetForCurrentThread();
            if (coreWindow != null)
            {
                coreWindow.PointerExited -= CoreWindow_PointerExited;
                coreWindow.Dispatcher.AcceleratorKeyActivated -= CustomTransportControls_AcceleratorKeyActivated;
            }

            ShowCursor();
        }

        private void CustomTransportControls_Loaded(object sender, RoutedEventArgs e)
        {
            hideViewTimer.Tick += HideViewTimer_Tick;
            hideInfoPanelTimer.Tick += HideInfoPanelTimer_Tick;
            togglePauseTimer.Tick += TogglePauseTimer_Tick;

            var coreWindow = CoreWindow.GetForCurrentThread();
            if (coreWindow != null)
            {
                coreWindow.PointerExited += CoreWindow_PointerExited;
                coreWindow.Dispatcher.AcceleratorKeyActivated += CustomTransportControls_AcceleratorKeyActivated;
            }

            EnsurePlayerMargin();
        }

        private void CustomTransportControls_AcceleratorKeyActivated(CoreDispatcher sender,
            AcceleratorKeyEventArgs args)
        {
            ShowView();
        }

        private async void TogglePauseTimer_Tick(object? sender, object e)
        {
            togglePauseTimer.Stop();
            if (playerControlReference.TryGetTarget(out var playerControl))
            {
                await playerControl.TogglePlayPauseAsync();
            }
        }

        private void HideInfoPanelTimer_Tick(object? sender, object e)
        {
            hideInfoPanelTimer.Stop();
            HideInfoPanel();
        }

        private void HideViewTimer_Tick(object? sender, object e)
        {
            hideViewTimer.Stop();
            HideView();
        }

        private void EnsurePlayerMargin()
        {
            if (playerControlReference.TryGetTarget(out var playerControl)
                && playerControl.WindowMode == WindowMode.FullScreen)
            {
                Margin = new Thickness(
                    Settings.Instance.ApplicationMarginLeft,
                    Settings.Instance.ApplicationMarginTop,
                    Settings.Instance.ApplicationMarginRight,
                    Settings.Instance.ApplicationMarginBottom);
            }
            else
            {
                Margin = new Thickness();
            }
        }

        private void CoreWindow_PointerExited(CoreWindow sender, PointerEventArgs args)
        {
            if (args.CurrentPoint?.PointerDevice?.PointerDeviceType == PointerDeviceType.Mouse
                && hideViewTimer.IsEnabled
                && playerControlReference.TryGetTarget(out _)
                && !VisualTreeHelper.GetOpenPopups(Window.Current).Any())
            {
                HideView();
            }
        }

        private void VideoOpening(PlayerControl _, VideoEventArgs args)
        {
            mediaMainControls!.IsLoading = true;
        }

        private void VideoOpened(PlayerControl sender, VideoEventArgs e)
        {
            mediaMainControls!.IsLoading = false;

            lastPlayingVideo = e.Video;

            mediaSettings!.SubtitleTracks = sender.SubtitleTracks;
            mediaSettings.AudioTracks = sender.AudioTracks ?? TrackCollection.Empty<AudioTrack>();
            mediaSettings.Videos = sender.PlayingFile?.Videos ?? Array.Empty<Video>();

            mediaSettings.CurrentSubtitleTrack = sender.CurrentSubtitleTrack;
            mediaSettings.CurrentAudioTrack = sender.CurrentAudioTrack;
            mediaSettings.CurrentVideo = e.Video;

            mediaMainControls!.Duration = sender.Duration ?? TimeSpan.Zero;

            mediaHeader!.CurrentFile = sender.PlayingFile;

            mediaPlaylist!.ItemsSource = sender.PlayingFile?.Playlist ?? new List<File>();
            mediaPlaylist.SelectedItem = sender.PlayingFile;
        }

        private void BufferingChanged(PlayerControl _, BufferingEventArgs e)
        {
            mediaMainControls!.BufferingPosition = e.Active ? e.Progress : 1d;
        }

        private void BufferedRangesChanged(PlayerControl sender, BufferedRangesChangedEventArgs args)
        {
            mediaMainControls!.BufferedRanges = args.Ranges;
        }

        private void PlaybackRateChanged(PlayerControl _, PlaybackRateEventArgs e)
        {
            mediaSettings!.PlaybackRate = e.PlaybackRate;

            infoTextBlock!.Text = e.PlaybackRate.ToString("0.##x", CultureInfo.InvariantCulture);

            ShowInfoPanel();
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (playerControlReference.TryGetTarget(out var playerControl)
                && playerControl.WindowMode == WindowMode.FullScreen
                && e.GetCurrentPoint(this).Position.X is var xPos
                && (xPos <= 0 || (ActualWidth - xPos) < 2))
            {
                HideView();
            }
            else if (!IsGeneratedAvaiable || !e.IsGenerated)
            {
                ShowView();
            }
        }

        private void StateChanged(PlayerControl _, PlayerStateEventArgs args)
        {
            var isPlaying = args.State == MediaPlaybackState.Playing;
            isPlayingOrPaused = isPlaying || args.State == MediaPlaybackState.Paused;
            if (isPlayingOrPaused)
            {
                hideViewTimer.Start();
            }

            var canPause = isPlaying || args.State == MediaPlaybackState.Buffering
                                     || args.State == MediaPlaybackState.Opening;
            VisualStateManager.GoToState(mediaMainControls, canPause
                ? "PauseState"
                : "PlayState", false);

            if (!isPlaying)
            {
                ShowView();

                if (args.State == MediaPlaybackState.None
                    || args.State == MediaPlaybackState.Opening)
                {
                    mediaMainControls!.Position = TimeSpan.Zero;
                }
            }
        }

        private void PositionChanged(PlayerControl _, PositionEventArgs args)
        {
            mediaMainControls!.Duration = args.Duration ?? TimeSpan.Zero;
            mediaMainControls.Position = args.NewValue;

            if (!args.Delta.HasValue)
            {
                return;
            }

            if (args.ChangeType != PositionChangeType.Keyboard
                && args.ChangeType != PositionChangeType.Swipe
                && args.ChangeType != PositionChangeType.PlayerControl
                && args.ChangeType != PositionChangeType.SystemControl)
            {
                return;
            }

            lastDisplayedSeek += args.Delta.Value;

            infoTextBlock!.Inlines.Clear();
            infoTextBlock.Text = lastDisplayedSeek.ToFriendlyString(true, false);

            ShowInfoPanel();
        }

        private void VolumeChanged(PlayerControl _, VolumeEventArgs args)
        {
            infoTextBlock!.Inlines.Clear();
            infoTextBlock.Inlines.Add(new Run
            {
                Text = " ", FontSize = 24, FontFamily = new FontFamily("Segoe MDL2 Assets")
            });
            infoTextBlock.Inlines.Add(new Run {Text = (int)(args.NewValue * 100) + "%"});

            ShowInfoPanel();
        }

        private void WindowModeChanged(PlayerControl _, WindowModeChangedEventArgs args)
        {
            VisualStateManager.GoToState(
                mediaMainControls,
                args.WindowMode == WindowMode.FullScreen ? "FullScreenState" : "NonFullScreenState",
                false);

            EnsurePlayerMargin();

            if ((args.WindowMode != WindowMode.None
                 || touchCapabilities.TouchPresent == 0)
                && Settings.Instance.PlayerSwipeEnabled)
            {
                ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
            }
            else
            {
                ManipulationMode = ManipulationModes.System | ManipulationModes.TranslateY;
            }

            if (args.WindowMode != WindowMode.FullScreen)
            {
                ShowCursor();
            }
        }

        private void RootGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (e.OriginalSource != sender)
            {
                return;
            }

            if (Settings.Instance.PlayerPauseOnPrimaryTapped)
            {
                togglePauseTimer.Start();
                e.Handled = true;
            }
            else if (controlAnimationState == ControlAnimationState.Shown)
            {
                HideView();
            }
        }

        private void RootGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.OriginalSource != sender)
            {
                return;
            }

            if (Settings.Instance.PlayerPauseOnPrimaryTapped)
            {
                if (controlAnimationState == ControlAnimationState.Shown)
                {
                    HideView();
                }
            }
            else
            {
                togglePauseTimer.Start();
                e.Handled = true;
            }
        }

        private async void TogglePauseOnMiddleClick(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource != sender)
            {
                return;
            }

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse
                && e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                if (ManagedWindow.GetCurrent(Window.Current) is ManagedWindow managedWindow
                    && !managedWindow.IsMainWindow)
                {
                    e.Handled = await managedWindow.CloseAsync().ConfigureAwait(true);
                }
                else
                {
                    if (!togglePauseTimer.IsEnabled)
                    {
                        togglePauseTimer.Start();
                    }

                    e.Handled = true;
                }
            }
        }

        private void ToggleFullScreenOnDoubleTap(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (e.OriginalSource != sender)
            {
                return;
            }

            togglePauseTimer.Stop();

            if (playerControlReference.TryGetTarget(out var playerControl))
            {
                playerControl.ToggleFullscreen();

                e.Handled = true;
            }
        }

        private void WheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource != sender
                || !playerControlReference.TryGetTarget(out var playerControl))
            {
                return;
            }

            var properties = e.GetCurrentPoint((UIElement)sender).Properties;
            if (properties.IsHorizontalMouseWheel)
            {
                return;
            }

            var wheel = properties.MouseWheelDelta;

            var computedWheelDelta = ComputeDeltaFromWheel(wheel);
            playerControl.Volume = Math.Round(playerControl.Volume + computedWheelDelta, 2);
        }

        private void SwipeStart(object sender, ManipulationStartedRoutedEventArgs e)
        {
            if (e.OriginalSource != sender
                || !Settings.Instance.PlayerSwipeEnabled
                || !playerControlReference.TryGetTarget(out var playerControl))
            {
                return;
            }

            var container = (e.Container as FrameworkElement) ?? (sender as FrameworkElement);
            if (container != null)
            {
                var spacingBorderSize = 48;
                if (e.Position.X < spacingBorderSize
                    || e.Position.Y < spacingBorderSize
                    || e.Position.X > container.ActualWidth - spacingBorderSize
                    || e.Position.Y > container.ActualHeight - spacingBorderSize)
                {
                    return;
                }
            }

            startSwipePoint = null;
            startSwipeVolume = null;
            if (Math.Abs(e.Cumulative.Translation.Y) > Math.Abs(e.Cumulative.Translation.X))
            {
                currentGestureActionType = GestureActionType.Volume;
            }
            else
            {
                currentGestureActionType = GestureActionType.Seek;
            }

            startSwipePoint = e.Cumulative.Translation;
            startSwipeVolume = playerControl.Volume;
            lastSwipeSeekDelta = null;
            lastSwipeVolumeDelta = 0;
        }

        private void SwipeDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (e.OriginalSource != sender
                || !Settings.Instance.PlayerSwipeEnabled
                || currentGestureActionType == GestureActionType.None
                || !playerControlReference.TryGetTarget(out var playerControl))
            {
                return;
            }

            var delta = new Point(e.Cumulative.Translation.X - startSwipePoint?.X ?? 0,
                e.Cumulative.Translation.Y - startSwipePoint?.Y ?? 0);
            switch (currentGestureActionType)
            {
                case GestureActionType.Volume when delta.Y != 0:
                    var volumeDelta = ComputeVolumeDeltaFromGesture(delta);
                    if (Math.Abs((lastSwipeVolumeDelta ?? 0) - volumeDelta) < 0.01)
                    {
                        return;
                    }

                    lastSwipeVolumeDelta = volumeDelta;
                    playerControl.Volume = Math.Max(0d, Math.Min(1d, (startSwipeVolume ?? 0) + volumeDelta));
                    break;
                case GestureActionType.Seek when delta.X != 0:
                    infoBorder!.Visibility = Visibility.Visible;
                    var timespan = ComputeTimespanDeltaFromGesture(delta);

                    var newPosition = playerControl.Position + timespan;
                    if (newPosition < TimeSpan.Zero)
                    {
                        timespan = -playerControl.Position;
                    }
                    else if (newPosition > playerControl.Duration)
                    {
                        timespan = playerControl.Duration.Value - playerControl.Position;
                    }

                    lastSwipeSeekDelta = timespan;
                    infoTextBlock!.Text = timespan.ToFriendlyString(true, false);
                    break;
            }

            if (e.IsInertial)
            {
                e.Complete();
            }
        }

        private void SwipeCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (e.OriginalSource != sender
                || !Settings.Instance.PlayerSwipeEnabled
                || currentGestureActionType == GestureActionType.None
                || !startSwipePoint.HasValue
                || !playerControlReference.TryGetTarget(out var playerControl))
            {
                return;
            }

            var delta = new Point(e.Cumulative.Translation.X - startSwipePoint.Value.X,
                e.Cumulative.Translation.Y - startSwipePoint.Value.Y);
            switch (currentGestureActionType)
            {
                case GestureActionType.Volume when delta.Y != 0:
                    playerControl.Volume = Math.Max(0d,
                        Math.Min(1d, (startSwipeVolume ?? 0) + ComputeVolumeDeltaFromGesture(delta)));
                    break;
                case GestureActionType.Seek:
                    var seekDelta = lastSwipeSeekDelta ?? ComputeTimespanDeltaFromGesture(delta);

                    if (Math.Abs(seekDelta.TotalSeconds) >= 1)
                    {
                        playerControl.Seek(seekDelta, PositionChangeType.Swipe);
                    }
                    else
                    {
                        hideInfoPanelTimer.Start();
                    }

                    break;
            }

            currentGestureActionType = GestureActionType.None;
        }

        private void HideCursor()
        {
            if (playerControlReference.TryGetTarget(out var playerControl)
                && playerControl.WindowMode == WindowMode.FullScreen)
            {
                tempCursor = Window.Current.CoreWindow.PointerCursor;
                Window.Current.CoreWindow.PointerCursor = null;
            }
        }

        private void ShowCursor()
        {
            if (Window.Current.CoreWindow.PointerCursor == null)
            {
                Window.Current.CoreWindow.PointerCursor =
                    tempCursor ??
                    new CoreCursor(CoreCursorType.Arrow, 0);
            }
        }

        internal static double ComputeDeltaFromWheel(int wheel)
        {
            return wheelRatio * (PlayerControl.GetCurrentSeekModifier()) switch
            {
                SeekModifier.Double => wheel * 2,
                SeekModifier.Half => wheel * 0.5,
                _ => wheel,
            };
        }

        internal static double ComputeVolumeDeltaFromGesture(Point tranlation)
        {
            var volumeDelta = -tranlation.Y * volumeSwipeRatio * Settings.Instance.VolumeSwipeSensitive;

            return PlayerControl.GetCurrentSeekModifier() switch
            {
                SeekModifier.Double => volumeDelta * 2,
                SeekModifier.Half => volumeDelta * 0.5,
                _ => volumeDelta
            };
        }

        internal static TimeSpan ComputeTimespanDeltaFromGesture(Point tranlation)
        {
            var seconds = tranlation.X * seekSwipeRatio * Settings.Instance.SeekSwipeSensitive;

            return TimeSpan.FromSeconds(PlayerControl.GetCurrentSeekModifier() switch
            {
                SeekModifier.Double => seconds * 2,
                SeekModifier.Half => seconds * 0.5,
                _ => seconds
            });
        }
    }
}

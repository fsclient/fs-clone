namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Graphics.Display;
    using Windows.Graphics.Imaging;
    using Windows.Media.Playback;
    using Windows.Storage.Streams;
    using Windows.UI.Core;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Localization.Resources;
    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;

    using Humanizer;

    public sealed partial class PlayerControl : MediaPlayerElement
    {
        public const double PlaybackRateStep = 0.1;
        public const double MinPlaybackRate = PlaybackRateStep;
        public const double MaxPlaybackRate = 2;

        private const double volumeUpDelta = 0.02;
        private const double volumeDownDelta = 0.02;

        private readonly DispatcherTimer positionUpdater;
        private readonly DisplayService displayService;

        private static readonly ConditionalWeakTable<Window, MediaPlayer> playersPerDispather =
            new ConditionalWeakTable<Window, MediaPlayer>();

        private (Video video, IMediaPlaybackSource source, IFrameGrabber? frameGrabber)? currentPlaybackSourceVideo;

        private TimeSpan? lastTimerPosition;
        private DateTime lastPositionSavedTime;
        private bool hasPausedInBackground;
        private bool shouldPauseOnOpen;
        private bool shouldPreloadOnPlay;
        private int errorRepeatCount;

        private readonly MediaSourceState mediaSourceState;
        private readonly ManagedWindow managedWindow;
        private readonly Window nativeWindow;

        private WeakEventListener<PlayerControl, CoreDispatcher, AcceleratorKeyEventArgs>?
            acceleratorKeyActivatedListener;

        private readonly ITrackRestoreService trackRestoreService;
        private readonly IContentDialog<string, bool> confirmDialog;
        private readonly IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput> castDialog;
        private readonly ILauncherService launcherService;
        private readonly INotificationService notificationService;
        private readonly ISettingService settingService;

        public PlayerControl()
        {
            trackRestoreService = ViewModelLocator.Current.Resolve<ITrackRestoreService>();
            confirmDialog = ViewModelLocator.Current.ResolveDialog<string, bool>();
            castDialog = ViewModelLocator.Current.ResolveDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput>();
            launcherService = ViewModelLocator.Current.Resolve<ILauncherService>();
            notificationService = ViewModelLocator.Current.Resolve<INotificationService>();
            settingService = ViewModelLocator.Current.Resolve<ISettingService>();
            mediaSourceState =
                new MediaSourceState(ViewModelLocator.Current.Resolve<IDownloadManager>(), Logger.Instance);

            nativeWindow = Window.Current;
            managedWindow = ManagedWindow.GetCurrent(nativeWindow) ?? throw new InvalidOperationException("Invalid window");

            InitMediaPlayer();
            InitMediaElement();

            Background = new SolidColorBrush {Opacity = 0};

            displayService = new DisplayService();
            positionUpdater = new DispatcherTimer {Interval = TimeSpan.FromSeconds(0.9d)};
        }

        private void InitMediaPlayer()
        {
            _ = playersPerDispather.GetOrCreateValue(nativeWindow);
            MediaPlayerSafeInvoke(mediaPlayer =>
            {
                mediaPlayer.AutoPlay = true;
                mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Movie;
                mediaPlayer.PlaybackSession.PlaybackRate = PlaybackRate;
                mediaPlayer.Volume = Volume;

                mediaPlayer.CommandManager.NextBehavior.EnablingRule = MediaCommandEnablingRule.Always;
                mediaPlayer.CommandManager.PreviousBehavior.EnablingRule = MediaCommandEnablingRule.Always;
                mediaPlayer.CommandManager.ShuffleBehavior.EnablingRule = MediaCommandEnablingRule.Never;
                mediaPlayer.CommandManager.AutoRepeatModeBehavior.EnablingRule = MediaCommandEnablingRule.Never;
            });

            UnsubscribeFromMediaPlayerEvents();
        }

        private void UnsubscribeFromMediaPlayerEvents()
        {
            MediaPlayerSafeInvoke(mediaPlayer =>
            {
                mediaPlayer.CommandManager.FastForwardReceived -= CommandManager_FastForwardReceived;
                mediaPlayer.CommandManager.RewindReceived -= CommandManager_RewindReceived;
                mediaPlayer.CommandManager.NextReceived -= CommandManager_NextReceived;
                mediaPlayer.CommandManager.PreviousReceived -= CommandManager_PreviousReceived;
                mediaPlayer.CommandManager.RateReceived -= CommandManager_RateReceived;
                mediaPlayer.CommandManager.PlayReceived -= CommandManager_PlayReceived;
                mediaPlayer.CommandManager.PauseReceived -= CommandManager_PauseReceived;

                mediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
                mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
                mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
                mediaPlayer.CurrentStateChanged -= MediaPlayer_CurrentStateChanged;
                mediaPlayer.SeekCompleted -= MediaPlayer_SeekCompleted;

                mediaPlayer.PlaybackSession.BufferingProgressChanged -= PlaybackSession_BufferingProgressChanged;
                mediaPlayer.PlaybackSession.DownloadProgressChanged -= PlaybackSession_DownloadProgressChanged;
            });
        }

        private void SubscribeFromMediaPlayerEvents()
        {
            MediaPlayerSafeInvoke(mediaPlayer =>
            {
                mediaPlayer!.CommandManager.FastForwardReceived += CommandManager_FastForwardReceived;
                mediaPlayer.CommandManager.RewindReceived += CommandManager_RewindReceived;
                mediaPlayer.CommandManager.NextReceived += CommandManager_NextReceived;
                mediaPlayer.CommandManager.PreviousReceived += CommandManager_PreviousReceived;
                mediaPlayer.CommandManager.RateReceived += CommandManager_RateReceived;
                mediaPlayer.CommandManager.PlayReceived += CommandManager_PlayReceived;
                mediaPlayer.CommandManager.PauseReceived += CommandManager_PauseReceived;

                mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
                mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
                mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                mediaPlayer.CurrentStateChanged += MediaPlayer_CurrentStateChanged;
                mediaPlayer.SeekCompleted += MediaPlayer_SeekCompleted;

                mediaPlayer.PlaybackSession.BufferingProgressChanged += PlaybackSession_BufferingProgressChanged;
                mediaPlayer.PlaybackSession.DownloadProgressChanged += PlaybackSession_DownloadProgressChanged;
            });
        }

        private MediaPlaybackSession? TryGetPlaybackSessionOrNull()
        {
            try
            {
                if (playersPerDispather.TryGetValue(nativeWindow, out var mediaPlayer))
                {
                    return mediaPlayer.PlaybackSession;
                }
            }
            // Operation aborted
            catch (Exception ex) when (unchecked((uint)ex.HResult) == 0x80004004)
            {
                Logger.Instance.LogWarning(ex);
            }

            return null;
        }

        private bool MediaPlayerSafeInvoke(Action<MediaPlayer> action)
        {
            try
            {
                if (playersPerDispather.TryGetValue(nativeWindow, out var mediaPlayer))
                {
                    action(mediaPlayer);
                    return true;
                }
            }
            // Operation aborted
            catch (Exception ex) when (unchecked((uint)ex.HResult) == 0x80004004)
            {
                Logger.Instance.LogWarning(ex);
            }

            return false;
        }

        private async Task<bool> MediaPlayerSafeInvoke(Func<MediaPlayer, ValueTask> func)
        {
            try
            {
                if (playersPerDispather.TryGetValue(nativeWindow, out var mediaPlayer))
                {
                    await func(mediaPlayer).ConfigureAwait(false);
                    return true;
                }
            }
            // Operation aborted
            catch (Exception ex) when (unchecked((uint)ex.HResult) == 0x80004004)
            {
                Logger.Instance.LogWarning(ex);
            }

            return false;
        }

        private void PositionUpdater_Tick(object sender, object e)
        {
            try
            {
                if (TryGetPlaybackSessionOrNull()?.PlaybackState == MediaPlaybackState.Playing)
                {
                    OnPositionChanged(PositionChangeType.ByTime);
                }
            }
            // Operation aborted
            catch (Exception ex) when (unchecked((uint)ex.HResult) == 0x80004004)
            {
                Logger.Instance.LogWarning(ex);
            }
        }

        private async void PlaybackSession_BufferingProgressChanged(MediaPlaybackSession sender, object args)
        {
            var progress = sender.BufferingProgress;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => BufferingChanged?
                .Invoke(this, new BufferingEventArgs(progress)));
        }

        private async void PlaybackSession_DownloadProgressChanged(MediaPlaybackSession sender, object args)
        {
            if (Durations.Length > 1
                || Duration is not TimeSpan totalDuration)
            {
                return;
            }

            var range = new MediaRangedProgressBarRange(0, sender.DownloadProgress);
            var ranges = new[] {range};

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => BufferedRangesChanged?
                .Invoke(this, new BufferedRangesChangedEventArgs(ranges, totalDuration)));
        }

        private async void MediaPlayer_SeekCompleted(MediaPlayer sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () => OnPositionChanged(PositionChangeType.PlayerControl));
        }

        private async void CommandManager_PauseReceived(MediaPlaybackCommandManager sender,
            MediaPlaybackCommandManagerPauseReceivedEventArgs args)
        {
            args.Handled = true;
            var deferral = args.GetDeferral();
            try
            {
                await PauseAsync().ConfigureAwait(true);
            }
            finally
            {
                deferral.Complete();
            }
            args.Handled = true;
        }

        private async void CommandManager_PlayReceived(MediaPlaybackCommandManager sender,
            MediaPlaybackCommandManagerPlayReceivedEventArgs args)
        {
            args.Handled = true;
            var deferral = args.GetDeferral();
            try
            {
                await PlayAsync().ConfigureAwait(true);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void CommandManager_RateReceived(MediaPlaybackCommandManager sender,
            MediaPlaybackCommandManagerRateReceivedEventArgs args)
        {
            args.Handled = true;
            PlaybackRate = args.PlaybackRate;
        }

        private void CommandManager_PreviousReceived(MediaPlaybackCommandManager sender,
            MediaPlaybackCommandManagerPreviousReceivedEventArgs args)
        {
            args.Handled = GoPreviousCommand != null;
            GoPreviousCommand?.Execute();
        }

        private void CommandManager_NextReceived(MediaPlaybackCommandManager sender,
            MediaPlaybackCommandManagerNextReceivedEventArgs args)
        {
            args.Handled = GoNextCommand != null;
            GoNextCommand?.Execute();
        }

        private void CommandManager_RewindReceived(MediaPlaybackCommandManager sender,
            MediaPlaybackCommandManagerRewindReceivedEventArgs args)
        {
            args.Handled = Rewind(SeekModifier.Auto, PositionChangeType.SystemControl);
        }

        private void CommandManager_FastForwardReceived(MediaPlaybackCommandManager sender,
            MediaPlaybackCommandManagerFastForwardReceivedEventArgs args)
        {
            args.Handled = FastForward(SeekModifier.Auto, PositionChangeType.SystemControl);
        }

        private void InitMediaElement()
        {
            if (!playersPerDispather.TryGetValue(nativeWindow, out var mediaPlayer))
            {
                return;
            }

            SetMediaPlayer(mediaPlayer);
            AreTransportControlsEnabled = true;
            Loaded += MediaElement_Loaded;
            Unloaded += MediaElement_Unloaded;
            TransportControls = new CustomTransportControls(this) {IsCompact = Settings.Instance.CompactPlayer};

            SetupAccelerators();
        }

        private async void MediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromMediaPlayerEvents();
            SubscribeFromMediaPlayerEvents();

            acceleratorKeyActivatedListener =
                new WeakEventListener<PlayerControl, CoreDispatcher, AcceleratorKeyEventArgs>(this)
                {
                    OnEventAction = (pc, _, args) => pc.OnGlobalKeyPress(args),
                    OnDetachAction = (listener) =>
                        DispatcherHelper.GetForCurrentOrMainView().AcceleratorKeyActivated -= listener.OnEvent
                };
            DispatcherHelper.GetForCurrentOrMainView().AcceleratorKeyActivated +=
                acceleratorKeyActivatedListener.OnEvent;

            managedWindow.WindowModeChanged += ManagedWindow_WindowModeChanged;
            managedWindow.FocusChanged += ManagedWindow_FocusChanged;

            Application.Current.EnteredBackground += App_EnteredBackground;
            Application.Current.LeavingBackground += App_LeavingBackground;

            positionUpdater.Tick += PositionUpdater_Tick;

            // For case when control is loaded after unload
            if (!(TransportControls is CustomTransportControls))
            {
                TransportControls = new CustomTransportControls(this) {IsCompact = Settings.Instance.CompactPlayer};
            }

            Stretch = GetSavedStretch();
            RegisterPropertyChangedCallback(StretchProperty, OnStretchChanged);

            Stretch GetSavedStretch()
            {
                var saved = settingService.GetSetting(Settings.StateSettingsContainer, "PlayerStretch",
                    nameof(Stretch.Uniform));
                return Enum.TryParse(saved, out Stretch temp)
                    ? temp
                    : Stretch.Uniform;
            }

            static void OnStretchChanged(DependencyObject sender, DependencyProperty dp)
            {
                var playerControl = (PlayerControl)sender;
                playerControl.settingService.SetSetting(Settings.StateSettingsContainer, "PlayerStretch",
                    playerControl.Stretch.ToString());
            }

            if (PlayingVideo == null
                && CurrentVideo is Video currentVideo)
            {
                await SetVideoAsync(currentVideo);
            }
        }

        private void MediaElement_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromMediaPlayerEvents();

            acceleratorKeyActivatedListener?.Detach();

            managedWindow.FocusChanged -= ManagedWindow_FocusChanged;
            managedWindow.WindowModeChanged -= ManagedWindow_WindowModeChanged;

            Application.Current.EnteredBackground -= App_EnteredBackground;
            Application.Current.LeavingBackground -= App_LeavingBackground;

            positionUpdater.Tick -= PositionUpdater_Tick;

            DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;

            ClearSource(((MediaPlayerElement)sender).MediaPlayer);

            OnWindowModeChanged(WindowMode.None);

            CacheHelper.IsAutoCleanPaused = false;

            positionUpdater?.Stop();

            displayService?.ReleaseActive();

            TransportControls = null;
        }

        private async void App_LeavingBackground(object sender, Windows.ApplicationModel.LeavingBackgroundEventArgs e)
        {
            if (hasPausedInBackground
                && Settings.Instance.PauseOnBackgroundMedia)
            {
                await PlayAsync().ConfigureAwait(true);
            }
        }

        private async void App_EnteredBackground(object sender, Windows.ApplicationModel.EnteredBackgroundEventArgs e)
        {
            if (Settings.Instance.PauseOnBackgroundMedia
                && TryGetPlaybackSessionOrNull()?.PlaybackState == MediaPlaybackState.Playing)
            {
                hasPausedInBackground = true;
                await PauseAsync().ConfigureAwait(true);
            }
        }

        private async void ManagedWindow_FocusChanged(object sender, FocusChangedEventArgs e)
        {
            await Dispatcher.CheckBeginInvokeOnUI(() =>
            {
                if (!e.IsFocused
                    && Settings.Instance.CompactOnFocusLosed
                    && TryGetPlaybackSessionOrNull()?.PlaybackState == MediaPlaybackState.Playing
                    && WindowMode == WindowMode.FullScreen)
                {
                    WindowMode = WindowMode.CompactOverlay;
                }
            });
        }

        private async void ManagedWindow_WindowModeChanged(object sender, WindowModeChangedEventArgs e)
        {
            await Dispatcher.CheckBeginInvokeOnUI(() =>
            {
                if (e.WindowMode == WindowMode.None
                    && WindowMode != WindowMode.None)
                {
                    WindowMode = e.WindowMode;
                    OnWindowModeChanged(e);
                }
            });
        }

        private void UpdateAudioTrackIndex()
        {
            var item = (currentPlaybackSourceVideo?.source as MediaPlaybackList)?.CurrentItem
                       ?? currentPlaybackSourceVideo?.source as MediaPlaybackItem;
            if (item != null
                && item.AudioTracks.Count is var count
                && count > 0)
            {
                var newIndex = CurrentAudioTrack?.Index >= 0 ? CurrentAudioTrack.Index : 0;
                item.AudioTracks.SelectedIndex = Math.Max(0, Math.Min(count - 1, newIndex));
            }
        }

        private async Task SetVideoAsync(Video? video)
        {
            try
            {
                mediaSourceState.ClearSubsState();
                currentPlaybackSourceVideo?.frameGrabber?.Dispose();
                currentPlaybackSourceVideo = null;
                lastTimerPosition = null;
                shouldPreloadOnPlay = false;
                errorRepeatCount = 0;

                MediaPlayerSafeInvoke(mediaPlayer => mediaPlayer.Source = null);

                if (video == null)
                {
                    return;
                }

                VideoOpening?.Invoke(this, new VideoEventArgs(video));
                BufferedRangesChanged?.Invoke(this,
                    new BufferedRangesChangedEventArgs(new[] {new MediaRangedProgressBarRange()}, default));

                var currentVariant = await mediaSourceState.GetAvailableVideoVariantAsync(video, CancellationToken.None)
                    .ConfigureAwait(true);
                if (currentVariant == null)
                {
                    await notificationService.ShowAsync(
                        Strings.PlayerControl_NoValidFileForPlayback,
                        NotificationType.Warning);
                    return;
                }

                IMediaPlaybackSource mediaPlaybackSource;
                IFrameGrabber? frameGrabber = null;
                var source = await mediaSourceState.CreateFromVariantAsync(currentVariant, video).ConfigureAwait(true);
                if (source == null)
                {
                    await notificationService.ShowAsync(
                        Strings.PlayerControl_ThatVideoIsNotSupported,
                        NotificationType.Warning);
                    return;
                }
                else
                {
                    mediaPlaybackSource = source.Value.source;
                    MediaPlayerSafeInvoke(mediaPlayer => mediaPlayer.Source = mediaPlaybackSource);

                    if (MediaPlayerFrameGrabber.IsSupported)
                    {
                        if (source.Value.file != null)
                        {
                            frameGrabber = new MediaClipFrameGrabber(source.Value.file, Logger.Instance);
                        }
                        else if (Settings.Instance.ThumbnailFromOnlineVideoSource)
                        {
                            var clonedSource = await mediaSourceState
                                .CreateFromVariantAsync(currentVariant, video, ignoreSubs: true).ConfigureAwait(true);
                            if (clonedSource?.source is IMediaPlaybackSource clonedPlaybackSource)
                            {
                                frameGrabber = new MediaPlayerFrameGrabber(clonedPlaybackSource);
                            }
                        }
                    }
                }

                currentPlaybackSourceVideo = (video, mediaPlaybackSource, frameGrabber);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private async Task ApplySubtitlesAsync(SubtitleTrack? value)
        {
            if (currentPlaybackSourceVideo?.source is not IMediaPlaybackSource source)
            {
                return;
            }

            if (TryGetPlaybackSessionOrNull()?.PlaybackState != MediaPlaybackState.Playing)
            {
                var tcs = new TaskCompletionSource<bool>();

                StateChanged += PlayerControl_StateChanged;

                void PlayerControl_StateChanged(object sender, PlayerStateEventArgs e)
                {
                    if (e.State == MediaPlaybackState.Playing)
                    {
                        StateChanged -= PlayerControl_StateChanged;
                        tcs.TrySetResult(true);
                    }
                }

                await tcs.Task.ConfigureAwait(true);
            }

            await mediaSourceState.ApplySubtitlesAsync(source, value).ConfigureAwait(true);
        }

        private TrackCollection<AudioTrack> GetAudioTracks()
        {
            var item = (currentPlaybackSourceVideo?.source as MediaPlaybackList)?.CurrentItem
                       ?? currentPlaybackSourceVideo?.source as MediaPlaybackItem;
            if (item == null)
            {
                return TrackCollection.Empty<AudioTrack>();
            }

            var collection = new TrackCollection<AudioTrack>();

            var length = item.AudioTracks.Count;
            for (var i = 0; i < length; i++)
            {
                var trackLanguage = item.AudioTracks[i].Language;
                if (!string.IsNullOrEmpty(trackLanguage))
                {
                    trackLanguage = trackLanguage[0].ToString().ToUpperInvariant() + trackLanguage.Substring(1);
                }

                var track = PlayingFile?.EmbededAudioTracks.FirstOrDefault(t => t.Index == i)
                            ?? new AudioTrack(trackLanguage.NotEmptyOrNull());
                collection.Add(track);
            }

            return collection;
        }

        public Task<IRandomAccessStream?> GrabFrameStreamAsync(TimeSpan position, BitmapSize desiredSize,
            CancellationToken cancellationToken)
        {
            if (currentPlaybackSourceVideo?.frameGrabber is not IFrameGrabber grabber)
            {
                return Task.FromResult<IRandomAccessStream?>(null);
            }

            return grabber.GrabAsync(position, desiredSize, cancellationToken);
        }

        public async Task<IRandomAccessStream?> GrabCurrentFrameStreamAsync()
        {
            if (!currentPlaybackSourceVideo.HasValue
                || TryGetPlaybackSessionOrNull() is not MediaPlaybackSession mediaPlaybackSession
                || !playersPerDispather.TryGetValue(nativeWindow, out var mediaPlayer)
                || mediaPlaybackSession.NaturalVideoHeight == 0
                || mediaPlaybackSession.NaturalVideoWidth == 0
                || !MediaPlayerFrameGrabber.IsSupported)
            {
                return null;
            }

            try
            {
                await PauseAsync();
                var size = new BitmapSize()
                {
                    Height = mediaPlaybackSession.NaturalVideoHeight, Width = mediaPlaybackSession.NaturalVideoWidth
                };
                return await MediaPlayerFrameGrabber.GrabFromPlayerMomentAsync(mediaPlayer, size).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
                return null;
            }
        }

        public ValueTask PlayAsync()
        {
            return new ValueTask(MediaPlayerSafeInvoke(mediaPlayer =>
            {
                if (PlayingFile is not File playingFile
                    || mediaPlayer == null)
                {
                    return new ValueTask();
                }

                if ((shouldPreloadOnPlay
                     || mediaPlayer.Source == null
                     || TryGetPlaybackSessionOrNull() is not MediaPlaybackSession mediaPlaybackSession
                     || mediaPlaybackSession.PlaybackState == MediaPlaybackState.None)
                    && SetAndPreloadFileCommand?.CanExecute(playingFile) == true)
                {
                    shouldPreloadOnPlay = false;
                    return ExecuteAndPlayAsync();

                    async ValueTask ExecuteAndPlayAsync()
                    {
                        await SetAndPreloadFileCommand.ExecuteAsync(playingFile).ConfigureAwait(true);
                        mediaPlayer.Play();
                    }
                }

                mediaPlayer.Play();
                return new ValueTask();
            }));
        }

        public ValueTask PauseAsync()
        {
            MediaPlayerSafeInvoke(mediaPlayer => mediaPlayer.Pause());

            return new ValueTask();
        }

        public async ValueTask StopAsync()
        {
            var stopped = MediaPlayerSafeInvoke(mediaPlayer => mediaPlayer.Source = null);
            if (stopped
                && PlayingVideo != null
                && HandleVideoStopedCommand?.CanExecute(PlayingVideo) == true)
            {
                await HandleVideoStopedCommand.ExecuteAsync(PlayingVideo);
            }
        }

        public void ToggleFullscreen()
        {
            WindowMode = WindowMode == WindowMode.FullScreen ? WindowMode.None : WindowMode.FullScreen;
        }

        public ValueTask TogglePlayPauseAsync()
        {
            if (TryGetPlaybackSessionOrNull() is not MediaPlaybackSession mediaPlaybackSession)
            {
                return new ValueTask();
            }

            return mediaPlaybackSession.PlaybackState switch
            {
                MediaPlaybackState.Paused => PlayAsync(),
                MediaPlaybackState.None => PlayAsync(),
                MediaPlaybackState.Playing => PauseAsync(),
                MediaPlaybackState.Buffering => PauseAsync(),
                MediaPlaybackState.Opening => PauseAsync(),
                _ => throw new NotSupportedException($"PlaybackState.{mediaPlaybackSession.PlaybackState} is not supported"),
            };
        }

        public async void OpenCastToDialog()
        {
            await MediaPlayerSafeInvoke(async mediaPlayer =>
            {
                await PauseAsync().ConfigureAwait(true);
                var result = await castDialog
                    .ShowAsync(
                        new RemoteLaunchDialogInput(null, PlayingVideo?.SingleLink, true,
                            mediaPlayer.GetAsCastingSource()), default).ConfigureAwait(true);
                if (result.IsSuccess)
                {
                    await PlayAsync().ConfigureAwait(false);
                }
                else if (result.Error is { } error)
                {
                    await notificationService.ShowAsync(error, NotificationType.Error).ConfigureAwait(false);
                }
            });
        }

        private void ClearSource(MediaPlayer? player = null)
        {
            MediaPlayerSafeInvoke(mediaPlayer =>
            {
                player ??= mediaPlayer;
                if (player?.Source != null)
                {
                    player.Source = null;
                }

                if (currentPlaybackSourceVideo?.source is MediaPlaybackList list)
                {
                    foreach (var item in list.Items)
                    {
                        item.Source?.Dispose();
                    }
                }
                else if (currentPlaybackSourceVideo?.source is MediaPlaybackItem item)
                {
                    item.Source?.Dispose();
                }

                currentPlaybackSourceVideo?.frameGrabber?.Dispose();

                currentPlaybackSourceVideo = null;
            });
        }

        private async void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (sender.Volume == 0
                    || sender.IsMuted)
                {
                    _ = notificationService.ShowAsync(
                        Strings.PlayerControl_SoundMutedInPlayer,
                        NotificationType.Warning);
                }

                if (currentPlaybackSourceVideo?.source is MediaPlaybackList list)
                {
                    Durations = list
                        .Items
                        .Select(item => item.Source?.Duration ?? TimeSpan.Zero)
                        .ToArray();
                }
                else if (TryGetPlaybackSessionOrNull()?.NaturalDuration is TimeSpan mediaDuration)
                {
                    Duration = mediaDuration;
                }
                else
                {
                    Duration = null;
                }

                var (startPosition, duration) = await SetupPlayerPositionAsync().ConfigureAwait(true);

                if (PlayingVideo != null
                    && PlayingVideo == CurrentVideo)
                {
                    AudioTracks = GetAudioTracks();

                    CurrentSubtitleTrack = trackRestoreService.Restore(SubtitleTracks, PlayingFile);
                    CurrentAudioTrack = trackRestoreService.Restore(AudioTracks, PlayingFile) ??
                                        AudioTracks.FirstOrDefault();

                    VideoOpened?.Invoke(this, new VideoEventArgs(PlayingVideo));
                    OnPositionChanged(PositionChangeType.ByTime);
                }

                if (shouldPauseOnOpen)
                {
                    await PauseAsync().ConfigureAwait(true);
                    shouldPauseOnOpen = false;
                }
            });

            async ValueTask<(TimeSpan startPosition, TimeSpan duration)> SetupPlayerPositionAsync()
            {
                var duration = Duration;

                var file = PlayingVideo?.ParentFile;
                if (file != null
                    && duration.HasValue
                    && !float.IsNaN(file.Position)
                    && file.Position > 0
                    && file.Position < 1)
                {
                    var position =
                        TimeSpan.FromMilliseconds(
                            Math.Max(0, (file.Position * duration.Value.TotalMilliseconds) - 2000));
                    Position = position;
                    await PlayAsync().ConfigureAwait(true);
                    return (position, duration.Value);
                }

                return (default, duration ?? default);
            }
        }

        private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            await Dispatcher.RunTaskAsync(async () =>
            {
                var lastPlayingVideo = PlayingVideo;
                shouldPreloadOnPlay = true;

                OnPositionChanged(PositionChangeType.ByTime, true);
                if (Settings.Instance.OpenNextVideo)
                {
                    if (GoNextCommand?.CanExecute() == true)
                    {
                        await GoNextCommand.ExecuteAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        PlaylistEnded?.Invoke(this, EventArgs.Empty);
                    }
                }
                // GoNextCommand will call HandleVideoStopedCommand internally
                else
                {
                    if (lastPlayingVideo != null
                        && HandleVideoStopedCommand?.CanExecute(lastPlayingVideo) == true)
                    {
                        await HandleVideoStopedCommand.ExecuteAsync(lastPlayingVideo).ConfigureAwait(true);
                    }
                }
            });
        }

        private async void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs e)
        {
            await Dispatcher.RunTaskAsync(async () =>
            {
                try
                {
                    OnPositionChanged(PositionChangeType.ByTime);
                    if (e.ExtendedErrorCode != null)
                    {
                        Logger.Instance.LogWarning(e.ExtendedErrorCode);
                    }

                    var currentVideo = PlayingVideo;
                    if (currentVideo == null)
                    {
                        return;
                    }

                    if (e.Error == MediaPlayerError.SourceNotSupported
                        && !string.IsNullOrWhiteSpace(e.ErrorMessage))
                    {
                        var missedFeatures = await ViewModelLocator.Current.Resolve<ISystemFeaturesService>()
                            .GetMissedFeaturesAsync().ConfigureAwait(true);
                        foreach (var feature in missedFeatures)
                        {
                            var result = await confirmDialog
                                .ShowAsync(
                                    Strings.ConfirmDialog_OpenMissedFeatureLink
                                        .FormatWith(feature.LocalizedFeatureName), CancellationToken.None)
                                .ConfigureAwait(true);
                            if (result)
                            {
                                await launcherService.LaunchUriAsync(feature.FeatureInstallLink).ConfigureAwait(true);
                            }
                        }
                    }
                    else if (e.Error == MediaPlayerError.DecodingError
                             && errorRepeatCount < 3)
                    {
                        errorRepeatCount++;
                        CurrentVideo = currentVideo;
                    }
                    else
                    {
                        var lowerVideo = currentVideo.ParentFile?.GetLowerQualityVideo(currentVideo);

                        if (lowerVideo != null)
                        {
                            CurrentVideo = lowerVideo;
                        }
                        else if (errorRepeatCount++ < 2)
                        {
                            CurrentVideo = currentVideo;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError(ex);
                }
            });
        }

        private void UpdateDisplayStatus()
        {
            if (displayService != null)
            {
                if (TryGetPlaybackSessionOrNull()?.PlaybackState == MediaPlaybackState.Playing
                    && WindowMode == WindowMode.FullScreen)
                {
                    displayService.HoldActive();
                }
                else
                {
                    displayService.ReleaseActive();
                }
            }
        }

        private void UpdatePositionUpdaterStatus()
        {
            if (positionUpdater != null)
            {
                if (TryGetPlaybackSessionOrNull()?.PlaybackState == MediaPlaybackState.Playing)
                {
                    if (!positionUpdater.IsEnabled)
                    {
                        positionUpdater.Start();
                    }
                }
                else
                {
                    if (positionUpdater.IsEnabled)
                    {
                        positionUpdater.Stop();
                    }
                }
            }
        }

        private async void MediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        {
            await Dispatcher.RunTaskAsync(async () =>
            {
                if (TryGetPlaybackSessionOrNull() is not MediaPlaybackSession mediaPlaybackSession)
                {
                    return;
                }

                var lastPlayingVideo = PlayingVideo;

                UpdateDisplayStatus();

                UpdatePositionUpdaterStatus();

                if (mediaPlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                {
                    mediaPlaybackSession.PlaybackRate = PlaybackRate;
                }

                CacheHelper.IsAutoCleanPaused = mediaPlaybackSession.PlaybackState == MediaPlaybackState.Playing;

                if (mediaPlaybackSession.PlaybackState == MediaPlaybackState.None
                    && lastPlayingVideo != null
                    && HandleVideoStopedCommand?.CanExecute(lastPlayingVideo) == true)
                {
                    await HandleVideoStopedCommand.ExecuteAsync(lastPlayingVideo).ConfigureAwait(true);
                }

                StateChanged?.Invoke(this, new PlayerStateEventArgs(mediaPlaybackSession.PlaybackState));
            });
        }

        private void OnPositionChanged(PositionChangeType changeType = PositionChangeType.Unknown,
            bool ignoreThreshold = false)
        {
            OnPositionChanged(
                new PositionEventArgs(
                    Position,
                    changeType,
                    Duration),
                ignoreThreshold);
        }

        private async void OnPositionChanged(PositionEventArgs args, bool ignoreThreshold = false)
        {
            if (args.ChangeType == PositionChangeType.ByTime)
            {
                lastTimerPosition = args.NewValue;
            }

            if (args.Duration?.TotalMilliseconds > 0
                && CurrentVideo != null
                && CurrentVideo == PlayingVideo)
            {
                var pos = (float)(args.NewValue.TotalMilliseconds / args.Duration.Value.TotalMilliseconds);

                PositionChanged?.Invoke(this, args);
                var threshold = TimeSpan.FromSeconds(5);
                if (pos > float.Epsilon
                    && (ignoreThreshold || (DateTime.Now - lastPositionSavedTime) > threshold))
                {
                    lastPositionSavedTime = DateTime.Now;

                    if (SetPositionForCurrentCommand?.CanExecute() == true)
                    {
                        await SetPositionForCurrentCommand.ExecuteAsync(pos).ConfigureAwait(true);
                    }
                }
            }
        }

        private void OnVolumeChanged(VolumeEventArgs args)
        {
            settingService.SetSetting(Settings.StateSettingsContainer, "PlayerVolume", args.NewValue,
                SettingStrategy.Local);
            VolumeChanged?.Invoke(this, args);
        }

        private void OnPlaybackRateChanged(PlaybackRateEventArgs args)
        {
            settingService.SetSetting(Settings.StateSettingsContainer, nameof(PlaybackRate), args.PlaybackRate,
                SettingStrategy.Local);
            PlaybackRateChanged?.Invoke(this, args);
        }

        private async void OnWindowModeChanged(WindowMode newValue)
        {
            var result = await managedWindow
                .SetWindowModeAsync(newValue)
                .ConfigureAwait(true);

            if (result)
            {
                OnWindowModeChanged(new WindowModeChangedEventArgs(newValue));
            }
            else
            {
                WindowMode = WindowMode.None;
            }
        }

        private void OnWindowModeChanged(WindowModeChangedEventArgs e)
        {
            var pages = this.FindVisualAscendant<FrameworkElement>() != null
                // Fast path
                ? this.FindVisualAscendants<Page>()
                // Maximum 2 pages - MainPage and specific page in Frame
                : nativeWindow.Content.FindVisualChildren<Page>().Concat(new[] { nativeWindow.Content})
                    .OfType<Page>().Take(2);
            foreach (var page in pages)
            {
                var mode = e.WindowMode == WindowMode.None ? "NormalMode" : "FullWindowMode";
                VisualStateManager.GoToState(page, mode, false);
            }

            if (e.WindowMode == WindowMode.FullScreen)
            {
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape
                                                             | DisplayOrientations.LandscapeFlipped;
            }
            else
            {
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;
            }

            var lastFocusedElement = FocusManager.GetFocusedElement();
            if (TransportControls is MediaTransportControls mtc
                && (lastFocusedElement == null
                    || (e.WindowMode == WindowMode.FullScreen
                        && lastFocusedElement is FrameworkElement frameworkElement
                        && frameworkElement.IsChildOf(TransportControls))))
            {
                mtc.Focus(FocusState.Programmatic);
            }

            UpdateDisplayStatus();

            WindowModeChanged?.Invoke(this, e);
        }
    }
}

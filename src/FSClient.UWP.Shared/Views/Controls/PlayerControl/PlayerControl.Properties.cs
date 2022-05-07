namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Windows.Media.Playback;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.Shared;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Services;

    public partial class PlayerControl
    {
        public static readonly DependencyProperty WindowModeProperty =
            DependencyProperty.Register(nameof(WindowMode), typeof(WindowMode), typeof(PlayerControl),
                new PropertyMetadata(WindowMode.None, WindowMode_PropertyChanged));

        private static void WindowMode_PropertyChanged(DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (args.OldValue is WindowMode oldValue
                && args.NewValue is WindowMode newValue
                && newValue != oldValue)
            {
                ((PlayerControl)dependencyObject).OnWindowModeChanged(newValue);
            }
        }

        public bool IsFrameGrabberSupported => MediaPlayerFrameGrabber.IsSupported;

        public WindowMode WindowMode
        {
            get => (WindowMode)GetValue(WindowModeProperty);
            set => SetValue(WindowModeProperty, value);
        }

        public FlyoutBase? MoreFlyout
        {
            get => (TransportControls as CustomTransportControls)?.MoreFlyout;
            set
            {
                if (TransportControls is CustomTransportControls cts)
                {
                    cts.MoreFlyout = value;
                }
            }
        }

        public static readonly DependencyProperty PlaybackRateProperty =
            DependencyProperty.Register(nameof(PlaybackRate), typeof(double), typeof(PlayerControl),
                PropertyMetadata.Create(GetSavedPlaybackRate, PlaybackRate_Changed));

        private static object GetSavedPlaybackRate()
        {
            return ViewModelLocator.Current.Resolve<ISettingService>()
                .GetSetting(Settings.StateSettingsContainer, nameof(PlaybackRate), 1d);
        }

        private static void PlaybackRate_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var playerControl = (PlayerControl)d;
            var newValue = (double)e.NewValue;
            if (playerControl.TryGetPlaybackSessionOrNull() is { } playbackSession
                && Math.Abs(playbackSession.PlaybackRate - newValue) > 0.001)
            {
                playbackSession.PlaybackRate = newValue;
                playerControl.OnPlaybackRateChanged(new PlaybackRateEventArgs(newValue));
            }
        }

        public double PlaybackRate
        {
            get => (double)GetValue(PlaybackRateProperty);
            set => SetValue(PlaybackRateProperty, value);
        }

        public static readonly DependencyProperty VolumeProperty =
            DependencyProperty.Register(nameof(Volume), typeof(double), typeof(PlayerControl),
                PropertyMetadata.Create(GetSavedVolume, Volume_Changed));

        private static object GetSavedVolume()
        {
            return ViewModelLocator.Current.Resolve<ISettingService>()
                .GetSetting(Settings.StateSettingsContainer, "PlayerVolume", 1d);
        }

        private static void Volume_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var playerControl = (PlayerControl)d;
            playerControl.MediaPlayerSafeInvoke(mediaPlayer =>
            {
                var oldValue = mediaPlayer.Volume;
                var newValue = (double)e.NewValue;
                if (Math.Abs(oldValue - newValue) > 0.001)
                {
                    mediaPlayer.Volume = newValue;
                    playerControl!.OnVolumeChanged(new VolumeEventArgs(newValue, oldValue));
                }
            });
        }

        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, Math.Min(1, Math.Max(0, value)));
        }

        private SubtitleTrack? currentSubtitles;

        public TrackCollection<SubtitleTrack> SubtitleTracks =>
            PlayingFile?.SubtitleTracks ?? TrackCollection.Empty<SubtitleTrack>();

        public SubtitleTrack? CurrentSubtitleTrack
        {
            get => currentSubtitles;
            set
            {
                if (currentSubtitles != value)
                {
                    currentSubtitles = value;
                    trackRestoreService.Save(value, PlayingVideo?.ParentFile ?? PlayingFile);
                    _ = ApplySubtitlesAsync(value);
                }
            }
        }

        public TimeSpan Position
        {
            get
            {
                if (PlayingVideo == null
                    || !(TryGetPlaybackSessionOrNull() is { } playbackSession))
                {
                    return TimeSpan.Zero;
                }

                if (currentPlaybackSourceVideo?.source is MediaPlaybackList list)
                {
                    if (Durations?.Length != list.Items.Count)
                    {
                        return TimeSpan.Zero;
                    }

                    var currentIndex = list.CurrentItem == null ? 0 : list.CurrentItemIndex;
                    var calculatedPosition = playbackSession.Position;
                    for (var index = 0; index < list.Items.Count; index++)
                    {
                        if (currentIndex != index)
                        {
                            calculatedPosition += Durations[index];
                        }
                        else
                        {
                            break;
                        }
                    }

                    return calculatedPosition;
                }

                return playbackSession.Position;
            }
            set
            {
                if (PlayingVideo == null
                    || !(TryGetPlaybackSessionOrNull() is { } playbackSession))
                {
                    return;
                }

                if (currentPlaybackSourceVideo?.source is MediaPlaybackList list)
                {
                    var currentIndex = list.CurrentItem == null ? 0 : list.CurrentItemIndex;
                    var calculatedPosition = value;

                    list.CurrentItemChanged -= List_CurrentItemChanged;

                    if (Durations?.Length != list.Items.Count)
                    {
                        return;
                    }

                    for (var index = 0; index < list.Items.Count; index++)
                    {
                        var nextPosition = calculatedPosition - Durations[index];
                        if (nextPosition.TotalMilliseconds < 0
                            || index == list.Items.Count - 1)
                        {
                            if (currentIndex != index)
                            {
                                list.CurrentItemChanged += List_CurrentItemChanged;
                                list.MoveTo((uint)index);
                            }
                            else
                            {
                                playbackSession.Position = calculatedPosition;
                            }

                            break;
                        }

                        calculatedPosition = nextPosition;
                    }

                    void List_CurrentItemChanged(MediaPlaybackList sender,
                        CurrentMediaPlaybackItemChangedEventArgs args)
                    {
                        sender.CurrentItemChanged -= List_CurrentItemChanged;
                        if (args.Reason == MediaPlaybackItemChangedReason.AppRequested
                            && TryGetPlaybackSessionOrNull() is { } playbackSession)
                        {
                            playbackSession.Position = calculatedPosition;
                        }
                    }
                }
                else if (PlayingVideo != null)
                {
                    playbackSession.Position = value;
                }

                OnPositionChanged(new PositionEventArgs(
                    value,
                    PositionChangeType.Unknown,
                    Duration));
            }
        }

        private TimeSpan[] Durations { get; set; } = Array.Empty<TimeSpan>();

        public TimeSpan? Duration
        {
            get => Durations.Length == 1 ? Durations[0]
                : Durations.Length > 1 ? TimeSpan.FromMilliseconds(Durations.Sum(t => t.TotalMilliseconds))
                : (TimeSpan?)null;
            set => Durations = value == null ? Array.Empty<TimeSpan>() : new[] {value.Value};
        }

        public Video? PlayingVideo => currentPlaybackSourceVideo?.video;

        public File? PlayingFile => PlayingVideo?.ParentFile;

        public Video? CurrentVideo
        {
            get => (Video?)GetValue(CurrentVideoProperty);
            set => SetValue(CurrentVideoProperty, value);
        }

        public static readonly DependencyProperty CurrentVideoProperty =
            DependencyProperty.Register(nameof(CurrentVideo), typeof(Video), typeof(PlayerControl),
                new PropertyMetadata(null, OnCurrentVideoChanged));

        private static async void OnCurrentVideoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var playerControl = (PlayerControl)d;
            await playerControl.SetVideoAsync(e.NewValue as Video);
        }

        public IEnumerable<File>? Playlist => PlayingFile?.Playlist;

        public TrackCollection<AudioTrack>? AudioTracks { get; private set; }

        private AudioTrack? currentAudioTrack;

        public AudioTrack? CurrentAudioTrack
        {
            get => currentAudioTrack;
            set
            {
                if (currentAudioTrack != value)
                {
                    currentAudioTrack = value;
                    UpdateAudioTrackIndex();

                    trackRestoreService.Save(value, PlayingFile);
                }
            }
        }

        public AsyncCommand<Video>? HandleVideoStopedCommand { get; set; }

        public AsyncCommand? GoNextCommand { get; set; }

        public AsyncCommand? GoPreviousCommand { get; set; }

        public AsyncCommand<float>? SetPositionForCurrentCommand { get; set; }

        public AsyncCommand<File>? SetAndPreloadFileCommand { get; set; }
    }
}

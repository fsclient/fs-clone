namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
#endif

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;

    public partial class MediaSettings
    {
        public double MinPlaybackRate => PlayerControl.MinPlaybackRate;
        public double MaxPlaybackRate => PlayerControl.MaxPlaybackRate;
        public double PlaybackRateStep => PlayerControl.PlaybackRateStep;

        private double playbackRate;

        public double PlaybackRate
        {
            get => playbackRateSlider?.Value ?? playbackRate;
            set
            {
                playbackRate = value;
                if (playbackRateSlider != null)
                {
                    playbackRateSlider.Value = playbackRate;
                }
            }
        }

        private Stretch currentStretch = Stretch.None;

        public Stretch CurrentStretch
        {
            get => stretchSelector?.SelectedItem as Stretch? ?? currentStretch;
            set
            {
                currentStretch = value;
                if (stretchSelector != null)
                {
                    stretchSelector.SelectedItem = currentStretch;
                }
            }
        }

        private Video? currentVideo;

        public Video? CurrentVideo
        {
            get => (Video?)videoSelector?.SelectedItem ?? currentVideo;
            set
            {
                currentVideo = value;
                if (videoSelector != null)
                {
                    videoSelector.SelectedItem = currentVideo;
                }
            }
        }

        private SubtitleTrack? currentSubtitleTrack;

        public SubtitleTrack? CurrentSubtitleTrack
        {
            get => (SubtitleTrack?)subtitleTracksSelector?.SelectedItem ?? currentSubtitleTrack;
            set
            {
                currentSubtitleTrack = value ?? nullSubtitle;
                if (subtitleTracksSelector != null)
                {
                    subtitleTracksSelector.SelectedIndex = SubtitleTracks.IndexOf(currentSubtitleTrack);
                }
            }
        }

        private AudioTrack? currentAudioTrack;

        public AudioTrack? CurrentAudioTrack
        {
            get => (AudioTrack?)audioTracksSelector?.SelectedItem ?? currentAudioTrack;
            set
            {
                currentAudioTrack = value;
                if (audioTracksSelector != null)
                {
                    audioTracksSelector.SelectedIndex = AudioTracks.IndexOf(currentAudioTrack);
                }
            }
        }

        private IReadOnlyCollection<Video> videos = Array.Empty<Video>();

        public IReadOnlyCollection<Video> Videos
        {
            get => (videoSelector?.ItemsSource as IReadOnlyCollection<Video>) ?? videos;
            set
            {
                videos = value;
                if (videoSelector != null)
                {
                    videoSelector.ItemsSource = value;
                    videoSelector.Visibility = value.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private IReadOnlyCollection<SubtitleTrack> subtitleTracks = Array.Empty<SubtitleTrack>();

        public IReadOnlyCollection<SubtitleTrack> SubtitleTracks
        {
            get => (subtitleTracksSelector?.ItemsSource as IReadOnlyCollection<SubtitleTrack>) ?? subtitleTracks;
            set
            {
                subtitleTracks = value;
                if (subtitleTracksSelector != null)
                {
                    subtitleTracksSelector.ItemsSource = new SubtitleTrack[] {nullSubtitle}.Concat(value).ToList();
                    subtitleTracksSelector.Visibility = value.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private IReadOnlyCollection<AudioTrack> audioTracks = Array.Empty<AudioTrack>();

        public IReadOnlyCollection<AudioTrack> AudioTracks
        {
            get => (audioTracksSelector?.ItemsSource as IReadOnlyCollection<AudioTrack>) ?? audioTracks;
            set
            {
                audioTracks = value;
                if (audioTracksSelector != null)
                {
                    audioTracksSelector.ItemsSource = value;
                    audioTracksSelector.Visibility = value.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }
}

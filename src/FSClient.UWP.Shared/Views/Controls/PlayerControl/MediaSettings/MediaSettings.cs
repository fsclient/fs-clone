namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Linq;

#if WINUI3
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Data;
    using Microsoft.UI.Xaml.Media;
#else
    using Windows.UI.Xaml.Data;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.Localization.Resources;

    public partial class MediaSettings : Control
    {
        private static readonly SubtitleTrack nullSubtitle =
            new SubtitleTrack(null, new Uri("http://blank")) { Title = Strings.Subtitles_None };

        private Slider? playbackRateSlider;
        private ComboBox? stretchSelector;
        private ComboBox? videoSelector;
        private ComboBox? subtitleTracksSelector;
        private ComboBox? audioTracksSelector;

        public MediaSettings()
        {
            DefaultStyleKey = nameof(MediaSettings);
        }

        protected override void OnApplyTemplate()
        {
            playbackRateSlider = GetTemplateChild("PlaybackRateSlider") as Slider
                                 ?? throw new InvalidOperationException("PlaybackRateSlider element is missed");
            playbackRateSlider.ValueChanged += PlaybackRateSlider_ValueChanged;
            PlaybackRate = playbackRate;

            audioTracksSelector = GetTemplateChild("AudioTracksSelector") as ComboBox
                                  ?? throw new InvalidOperationException("AudioTracksSelector element is missed");
            audioTracksSelector.SelectionChanged += AudioTracksSelector_SelectionChanged;
            AudioTracks = audioTracks;
            CurrentAudioTrack = currentAudioTrack;

            videoSelector = GetTemplateChild("VideoSelector") as ComboBox
                            ?? throw new InvalidOperationException("VideoSelector element is missed");
            videoSelector.SelectionChanged += VideoSelector_SelectionChanged;
            Videos = videos;
            CurrentVideo = currentVideo;

            subtitleTracksSelector = GetTemplateChild("SubtitleTracksSelector") as ComboBox
                                     ?? throw new InvalidOperationException("SubtitleTracksSelector element is missed");
            subtitleTracksSelector.SelectionChanged += SubtitleTracksSelector_SelectionChanged;
            SubtitleTracks = subtitleTracks;
            CurrentSubtitleTrack = currentSubtitleTrack;

            stretchSelector = GetTemplateChild("StretchSelector") as ComboBox
                              ?? throw new InvalidOperationException("StretchSelector element is missed");
            stretchSelector.SelectionChanged += StretchSelector_SelectionChanged;
            stretchSelector.ItemsSource = new[] {Stretch.Fill, Stretch.Uniform, Stretch.UniformToFill};
            CurrentStretch = currentStretch;

            base.OnApplyTemplate();
        }

        private void PlaybackRateSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (((Slider)sender).IsLoaded()
                // Ignore initial state changed
                && e.OldValue >= MinPlaybackRate)
            {
                PlaybackRateChangeRequested?.Invoke(sender, new EventArgs<double>(e.NewValue));
            }
        }

        private void StretchSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox)sender).IsLoaded()
                && !e.RemovedItems.SequenceEqual(e.AddedItems)
                && e.AddedItems.Count == 1
                && e.RemovedItems.Count == 1)
            {
                StretchChangeRequested?.Invoke(sender, new EventArgs<Stretch>((Stretch)e.AddedItems[0]));
            }
        }

        private void SubtitleTracksSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox)sender).IsLoaded()
                && !e.RemovedItems.SequenceEqual(e.AddedItems)
                && e.AddedItems.Count == 1
                && e.RemovedItems.Count == 1)
            {
                var selected = (SubtitleTrack)e.AddedItems[0];
                SubtitleTrackChangeRequested?.Invoke(sender, new EventArgs<SubtitleTrack?>(
                    ReferenceEquals(nullSubtitle, selected) ? null : selected));
            }
        }

        private void AudioTracksSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox)sender).IsLoaded()
                && !e.RemovedItems.SequenceEqual(e.AddedItems)
                && e.AddedItems.Count == 1
                && e.RemovedItems.Count == 1)
            {
                AudioTrackChangeRequested?.Invoke(sender, new EventArgs<AudioTrack?>((AudioTrack)e.AddedItems[0]));
            }
        }

        private void VideoSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox)sender).IsLoaded()
                && !e.RemovedItems.SequenceEqual(e.AddedItems)
                && e.AddedItems.Count == 1
                && e.RemovedItems.Count == 1)
            {
                VideoChangeRequested?.Invoke(sender, new EventArgs<Video?>((Video)e.AddedItems[0]));
            }
        }
    }

    public class StretchTitleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, string? language)
        {
            return value switch
            {
                Stretch.None => Strings.Stretch_None,
                Stretch.Fill => Strings.Stretch_Fill,
                Stretch.Uniform => Strings.Stretch_Uniform,
                Stretch.UniformToFill => Strings.Stretch_UniformToFill,
                _ => null
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, string? language)
        {
            throw new NotSupportedException();
        }
    }
}

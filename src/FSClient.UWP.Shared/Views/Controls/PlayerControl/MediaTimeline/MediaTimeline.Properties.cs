namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;

#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    using FSClient.Shared;
    using FSClient.Shared.Helpers;

    public partial class MediaTimeline
    {
        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(MediaTimeline),
                new PropertyMetadata(false));

        public double BufferingPosition
        {
            get => bufferingProgressBar!.Value / 100;
            set
            {
                if (value <= 0 || value >= 1)
                {
                    bufferingProgressBar!.Visibility = Visibility.Collapsed;
                }
                else
                {
                    bufferingProgressBar!.Visibility = Visibility.Visible;
                    bufferingProgressBar.Value = value * 100;
                }
            }
        }

        public TimeSpan Position
        {
            get => progressSlider?.Position ?? TimeSpan.Zero;
            set
            {
                bufferingProgressBar!.Visibility = Visibility.Collapsed;
                if (value.TotalMilliseconds == 0)
                {
                    progressSlider!.Value = 0;
                    elapsedTimeElement!.Text = remainingTimeElement!.Text = durationTimeElement!.Text
                        = TimeSpan.Zero.ToFriendlyString(false, false);
                }
                else
                {
                    var forceHours = Duration.TotalHours >= 1;
                    elapsedTimeElement!.Text = value.ToFriendlyString(false, forceHours);
                    if (Settings.Instance.InvertElapsedPlayerTime)
                    {
                        durationTimeElement!.Text = (Duration - value).ToFriendlyString(false, forceHours);
                        remainingTimeElement!.Text = Duration.ToFriendlyString(false, forceHours);
                    }
                    else
                    {
                        durationTimeElement!.Text = Duration.ToFriendlyString(false, forceHours);
                        remainingTimeElement!.Text = (Duration - value).ToFriendlyString(false, forceHours);
                    }

                    positionChangind = true;
                    progressSlider!.Value = Duration > TimeSpan.Zero
                        ? value.TotalMilliseconds / Duration.TotalMilliseconds * progressSlider.Maximum
                        : 0;
                    positionChangind = false;
                }
            }
        }

        public TimeSpan Duration
        {
            get => progressSlider?.Duration ?? TimeSpan.Zero;
            set
            {
                if (progressSlider != null)
                {
                    progressSlider.Duration = value;
                }
            }
        }

        public IEnumerable<MediaRangedProgressBarRange> BufferedRanges
        {
            get => progressSlider!.BufferedRanges;
            set => progressSlider!.BufferedRanges = value;
        }
    }
}

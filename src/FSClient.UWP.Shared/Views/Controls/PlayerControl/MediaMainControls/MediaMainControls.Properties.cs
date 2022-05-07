namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    public partial class MediaMainControls
    {
        public FlyoutBase VolumeFlyout
        {
            get => (FlyoutBase)GetValue(VolumeFlyoutProperty);
            set => SetValue(VolumeFlyoutProperty, value);
        }

        public static readonly DependencyProperty VolumeFlyoutProperty =
            DependencyProperty.Register(nameof(VolumeFlyout), typeof(FlyoutBase), typeof(MediaMainControls),
                new PropertyMetadata(null));

        public FlyoutBase SettingsFlyout
        {
            get => (FlyoutBase)GetValue(SettingsFlyoutProperty);
            set => SetValue(SettingsFlyoutProperty, value);
        }

        public static readonly DependencyProperty SettingsFlyoutProperty =
            DependencyProperty.Register(nameof(SettingsFlyout), typeof(FlyoutBase), typeof(MediaMainControls),
                new PropertyMetadata(null));

        public bool IsLoading
        {
            get => mediaTimeline!.IsLoading;
            set => mediaTimeline!.IsLoading = value;
        }

        public double BufferingPosition
        {
            get => mediaTimeline!.BufferingPosition;
            set => mediaTimeline!.BufferingPosition = value;
        }

        public TimeSpan Position
        {
            get => mediaTimeline!.Position;
            set => mediaTimeline!.Position = value;
        }

        public TimeSpan Duration
        {
            get => mediaTimeline!.Duration;
            set => mediaTimeline!.Duration = value;
        }

        public IEnumerable<MediaRangedProgressBarRange> BufferedRanges
        {
            get => mediaTimeline!.BufferedRanges;
            set => mediaTimeline!.BufferedRanges = value;
        }
    }
}

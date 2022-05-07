namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    public partial class MediaSlider
    {
        private IEnumerable<MediaRangedProgressBarRange> bufferedRanges =
            Enumerable.Empty<MediaRangedProgressBarRange>();

        public UIElement SliderToolTipPopupAnchor { get; set; }

        public TimeSpan Position { get; set; }

        public TimeSpan Duration { get; set; }

        public IEnumerable<MediaRangedProgressBarRange> BufferedRanges
        {
            get => bufferedRanges;
            set
            {
                if (bufferedRanges != value)
                {
                    bufferedRanges = value;

                    if (mediaDownloadedBar != null)
                    {
                        var currentProgressValue = Value;
                        foreach (var range in bufferedRanges)
                        {
                            var startPercent = range.Start * 100;
                            var endPercent = range.End * 100;
                            if (startPercent < currentProgressValue && endPercent >= currentProgressValue)
                            {
                                mediaDownloadedBar.Value = endPercent;
                                return;
                            }
                        }

                        mediaDownloadedBar.Value = 0;
                    }
                }
            }
        }
    }
}

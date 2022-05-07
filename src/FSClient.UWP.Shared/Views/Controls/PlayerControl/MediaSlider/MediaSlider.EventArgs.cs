namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Threading;

    using Windows.Graphics.Imaging;
    using Windows.Storage.Streams;

    using Nito.AsyncEx;

    public class MediaSliderThumbnailRequestedEventArgs : EventArgs
    {
        public MediaSliderThumbnailRequestedEventArgs(IDeferralSource deferralSource, TimeSpan position,
            BitmapSize size, CancellationToken cancellationToken)
        {
            DeferralSource = deferralSource;
            Position = position;
            Size = size;
            CancellationToken = cancellationToken;
        }

        public IDeferralSource DeferralSource { get; }

        public TimeSpan Position { get; }

        public BitmapSize Size { get; }

        public CancellationToken CancellationToken { get; }

        public IRandomAccessStream? ThumbnailImage { get; set; }
    }
}

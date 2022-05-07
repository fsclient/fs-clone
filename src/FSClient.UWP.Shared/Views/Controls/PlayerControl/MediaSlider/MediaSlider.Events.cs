namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

    using Windows.Foundation;

    using FSClient.Shared.Helpers;

    public partial class MediaSlider
    {
        public event EventHandler<EventArgs<TimeSpan>>? PositionChangeRequested;

        public event EventHandler<EventArgs<TimeSpan>>? SeekRequested;

        public event TypedEventHandler<MediaSlider, MediaSliderThumbnailRequestedEventArgs>? ThumbnailRequested;
    }
}

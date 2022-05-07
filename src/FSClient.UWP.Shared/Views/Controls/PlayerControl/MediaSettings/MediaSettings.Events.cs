namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

#if WINUI3
    using Microsoft.UI.Xaml.Media;
#else
    using Windows.UI.Xaml.Media;
#endif

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;

    public partial class MediaSettings
    {
        public event EventHandler<EventArgs<double>>? PlaybackRateChangeRequested;

        public event EventHandler<EventArgs<Stretch>>? StretchChangeRequested;

        public event EventHandler<EventArgs<Video?>>? VideoChangeRequested;

        public event EventHandler<EventArgs<SubtitleTrack?>>? SubtitleTrackChangeRequested;

        public event EventHandler<EventArgs<AudioTrack?>>? AudioTrackChangeRequested;
    }
}

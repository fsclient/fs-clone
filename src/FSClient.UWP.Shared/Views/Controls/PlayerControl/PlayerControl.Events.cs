namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

    using Windows.Foundation;

    public partial class PlayerControl
    {
        public event TypedEventHandler<PlayerControl, VideoEventArgs>? VideoOpening;

        public event TypedEventHandler<PlayerControl, VideoEventArgs>? VideoOpened;

        public event TypedEventHandler<PlayerControl, PlayerStateEventArgs>? StateChanged;

        public event TypedEventHandler<PlayerControl, WindowModeChangedEventArgs>? WindowModeChanged;

        public event TypedEventHandler<PlayerControl, VolumeEventArgs>? VolumeChanged;

        public event TypedEventHandler<PlayerControl, PositionEventArgs>? PositionChanged;

        public event TypedEventHandler<PlayerControl, BufferedRangesChangedEventArgs>? BufferedRangesChanged;

        public event TypedEventHandler<PlayerControl, BufferingEventArgs>? BufferingChanged;

        public event TypedEventHandler<PlayerControl, PlaybackRateEventArgs>? PlaybackRateChanged;

        public event TypedEventHandler<PlayerControl, EventArgs>? PlaylistEnded;
    }
}

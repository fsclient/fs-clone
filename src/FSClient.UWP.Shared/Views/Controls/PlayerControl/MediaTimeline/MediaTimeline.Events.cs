namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

    using FSClient.Shared.Helpers;

    public partial class MediaTimeline
    {
        public event EventHandler<EventArgs<TimeSpan>>? PositionChangeRequested;

        public event EventHandler<EventArgs<TimeSpan>>? SeekRequested;

        public event EventHandler? FastForwardRequested;

        public event EventHandler? RewindRequested;
    }
}

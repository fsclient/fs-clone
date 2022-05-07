namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

    using FSClient.Shared.Helpers;

    public partial class MediaMainControls
    {
        public event EventHandler? StopRequested;

        public event EventHandler? PlayPauseToggleRequested;

        public event EventHandler<EventArgs<WindowMode>>? WindowModeToggleRequested;

        public event EventHandler<EventArgs<TimeSpan>>? PositionChangeRequested;

        public event EventHandler<EventArgs<TimeSpan>>? SeekRequested;

        public event EventHandler? FastForwardRequested;

        public event EventHandler? RewindRequested;

        public event EventHandler? CastToRequested;
    }
}

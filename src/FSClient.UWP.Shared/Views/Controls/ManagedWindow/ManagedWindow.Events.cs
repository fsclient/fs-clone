namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

    using Nito.AsyncEx;

    public partial class ManagedWindow
    {
        public event EventHandler<WindowModeChangedEventArgs>? WindowModeChanged;
        public event EventHandler<FocusChangedEventArgs>? FocusChanged;
        public event EventHandler<WindowShowedEventArgs>? Showed;
        public event EventHandler<IDeferralSource>? Initing;
        public event EventHandler? Initialized;
        public event EventHandler<IDeferralSource>? Destroying;
        public event EventHandler? Destroyed;
    }
}

namespace FSClient.UWP.Shared.Views.Controls
{
    public enum SeekModifier
    {
        Unknown,
        Normal = 1,
        Auto,
        Double,
        Half
    }

    public enum PositionChangeType
    {
        Unknown,
        Keyboard = 1,
        Swipe,
        SystemControl,
        PlayerControl,
        ByTime
    }

    public enum PlayerControlKeyBindingAction
    {
        OnGoBackRequested = 1,
        ToggleFullscreen,
        TogglePlayPause,
        ToggleIsMuted,
        Rewind,
        FastForward,
        VolumeIncrease,
        VolumeDecrease,
        PlaybackRateStepIncrease,
        PlaybackRateStepDecrease,
        GoPreviousMediaItem,
        GoNextMediaItem,
        ToggleTransportControlsView,
        GoToOnePreviousPosition,
        GoToStartPosition,
        GoTo10PercentPosition,
        GoTo20PercentPosition,
        GoTo30PercentPosition,
        GoTo40PercentPosition,
        GoTo50PercentPosition,
        GoTo60PercentPosition,
        GoTo70PercentPosition,
        GoTo80PercentPosition,
        GoTo90PercentPosition,
        GoToEndPosition
    }
}

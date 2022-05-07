namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.UWP.Shared.Helpers;

    public partial class MediaMainControls : Control
    {
        private MediaTimeline? mediaTimeline;

        public MediaMainControls()
        {
            DefaultStyleKey = nameof(MediaMainControls);
        }

        protected override void OnApplyTemplate()
        {
            mediaTimeline = GetTemplateChild("MediaTimeline") as MediaTimeline
                            ?? throw new InvalidOperationException("MediaTimeline element is missed");

            mediaTimeline.SeekRequested += (s, arg) =>
                SeekRequested?.Invoke(this, arg);
            mediaTimeline.PositionChangeRequested += (s, arg) =>
                PositionChangeRequested?.Invoke(this, arg);
            mediaTimeline.RewindRequested += (s, arg) =>
                RewindRequested?.Invoke(this, arg);
            mediaTimeline.FastForwardRequested += (s, arg) =>
                FastForwardRequested?.Invoke(this, arg);

            if (GetTemplateChild("FullScreenButton") is ButtonBase fullScreenButton)
            {
                fullScreenButton.Click += (s, a) => WindowModeToggleRequested?
                    .Invoke(this, new EventArgs<WindowMode>(WindowMode.FullScreen));
            }

            if (GetTemplateChild("CastButton") is ButtonBase castButton)
            {
                castButton.Click += (s, a) =>
                    CastToRequested?.Invoke(this, EventArgs.Empty);
            }

            if (GetTemplateChild("PlayPauseButton") is ButtonBase playPauseButton)
            {
                playPauseButton.Click += (s, a) =>
                    PlayPauseToggleRequested?.Invoke(this, EventArgs.Empty);
            }

            if (GetTemplateChild("PlayPauseButtonOnLeft") is ButtonBase playPauseButtonOnLeft)
            {
                playPauseButtonOnLeft.Click += (s, a) =>
                    PlayPauseToggleRequested?.Invoke(this, EventArgs.Empty);
            }

            if (GetTemplateChild("SeekBackwardButton") is ButtonBase backwardButton)
            {
                backwardButton.Click += (s, a) =>
                    RewindRequested?.Invoke(this, EventArgs.Empty);
            }

            if (GetTemplateChild("SeekForwardButton") is ButtonBase forwardButton)
            {
                forwardButton.Click += (s, a) =>
                    FastForwardRequested?.Invoke(this, EventArgs.Empty);
            }

            if (GetTemplateChild("OverlayButton") is ButtonBase overlayButton)
            {
                if (!ManagedWindow.OverlaySupported)
                {
                    IgnoreButton(overlayButton);
                }
                else
                {
                    overlayButton.Click += (s, a) => WindowModeToggleRequested?
                        .Invoke(this, new EventArgs<WindowMode>(WindowMode.CompactOverlay));
                }
            }

            if (GetTemplateChild("StopButton") is ButtonBase stopButtton)
            {
                if (!Settings.Instance.PlayerStopButtonEnabled)
                {
                    IgnoreButton(stopButtton);
                }
                else
                {
                    stopButtton.Click += (s, a) =>
                        StopRequested?.Invoke(this, EventArgs.Empty);
                }
            }

            base.OnApplyTemplate();

            static void IgnoreButton(ButtonBase button)
            {
                button.Visibility = Visibility.Collapsed;
                button.IsEnabled = false;
                button.Width = 0;
                button.IsTabStop = false;
            }
        }

        protected override async void OnGotFocus(RoutedEventArgs e)
        {
            if (FocusState == FocusState.Keyboard
                && GetTemplateChild("MediaTimeline") is Control mediaTimeline
                && mediaTimeline.Visibility != Visibility.Collapsed
                && !((FrameworkElement)e.OriginalSource).IsChildOf(this))
            {
                await mediaTimeline.TryFocusAsync(FocusState.Programmatic).ConfigureAwait(true);
            }

            base.OnGotFocus(e);
        }
    }
}

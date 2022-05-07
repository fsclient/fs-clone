namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Helpers;
    using FSClient.UWP.Shared.Helpers;
    public partial class MediaTimeline : Control
    {
        private MediaSlider? progressSlider;
        private TextBlock? elapsedTimeElement;
        private TextBlock? remainingTimeElement;
        private ProgressBar? bufferingProgressBar;
        private TextBlock? durationTimeElement;
        private bool positionChangind;
        private bool wasRemainingElementVisible;

        public MediaTimeline()
        {
            DefaultStyleKey = nameof(MediaTimeline);
        }

        protected override void OnApplyTemplate()
        {
            progressSlider = GetTemplateChild("MediaProgressSlider") as MediaSlider;
            if (progressSlider != null)
            {
                progressSlider.SliderToolTipPopupAnchor = this;
                progressSlider.SeekRequested += ProgressSlider_SeekRequested;
                progressSlider.PositionChangeRequested += ProgressSlider_PositionChangeRequested;
            }

            bufferingProgressBar = GetTemplateChild("BufferingProgress") as ProgressBar;
            remainingTimeElement = GetTemplateChild("RemainingTimeElement") as TextBlock;
            elapsedTimeElement = GetTemplateChild("ElapsedTimeElement") as TextBlock;
            durationTimeElement = GetTemplateChild("DurationTimeElement") as TextBlock;

            if (elapsedTimeElement != null)
            {
                elapsedTimeElement.Tapped += ElapsedTimeElement_Tapped;
            }

            if (remainingTimeElement != null
                && durationTimeElement != null
                && GetTemplateChild("TimeRemainingHoverBorder") is Border hoverBorder)
            {
                hoverBorder.Tapped += (s, a) => FastForwardRequested?.Invoke(this, EventArgs.Empty);
                hoverBorder.PointerEntered += HoverBorder_PointerEntered;
                hoverBorder.PointerExited += HoverBorder_PointerExited;
                hoverBorder.PointerCanceled += HoverBorder_PointerExited;
                hoverBorder.PointerCaptureLost += HoverBorder_PointerExited;
            }

            base.OnApplyTemplate();
        }

        protected override async void OnGotFocus(RoutedEventArgs args)
        {
            if (FocusState == FocusState.Keyboard
                && GetTemplateChild("MediaProgressSlider") is Control mediaProgressSlider
                && mediaProgressSlider.Visibility != Visibility.Collapsed
                && !((FrameworkElement)args.OriginalSource).IsChildOf(this))
            {
                await mediaProgressSlider.TryFocusAsync(FocusState.Programmatic).ConfigureAwait(true);
            }

            base.OnGotFocus(args);
        }

        private void ElapsedTimeElement_Tapped(object sender, TappedRoutedEventArgs _)
        {
            RewindRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ProgressSlider_SeekRequested(object sender, EventArgs<TimeSpan> args)
        {
            SeekRequested?.Invoke(this, args);
        }

        private void ProgressSlider_PositionChangeRequested(object sender, EventArgs<TimeSpan> args)
        {
            if (!positionChangind)
            {
                PositionChangeRequested?.Invoke(this, args);
            }
        }

        private void HoverBorder_PointerEntered(object sender, PointerRoutedEventArgs _)
        {
            if (Duration.TotalMilliseconds > 0
                && durationTimeElement != null
                && remainingTimeElement != null)
            {
                durationTimeElement.Visibility = Visibility.Visible;
                wasRemainingElementVisible = remainingTimeElement.Visibility == Visibility.Visible;
                if (wasRemainingElementVisible)
                {
                    remainingTimeElement.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void HoverBorder_PointerExited(object sender, PointerRoutedEventArgs _)
        {
            durationTimeElement!.Visibility = Visibility.Collapsed;
            if (wasRemainingElementVisible)
            {
                remainingTimeElement!.Visibility = Visibility.Visible;
            }
        }
    }
}

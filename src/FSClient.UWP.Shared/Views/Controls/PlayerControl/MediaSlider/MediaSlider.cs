namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

    using Windows.Devices.Input;
    using Windows.Foundation;
    using Windows.Foundation.Metadata;
    using Windows.Graphics.Imaging;
    using Windows.Storage.Streams;
    using Windows.System;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Microsoft.UI.Xaml.Shapes;
    using Popup = Microsoft.UI.Xaml.Controls.Primitives.Popup;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
    using Windows.UI.Xaml.Shapes;
    using Windows.UI.Xaml.Media.Imaging;
    using Popup = Windows.UI.Xaml.Controls.Primitives.Popup;
#endif

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Nito.AsyncEx;

    public partial class MediaSlider : Slider
    {
        private readonly MouseCapabilities mouseCapabilities;
        private ProgressBar? mediaDownloadedBar;
        private TextBlock? sliderToolTipTextBlock;
        private Image? sliderToolTipThumbnailImage;
        private Popup? sliderToolTipPopup;
        private Rectangle? sliderRectangle;
        private Thumb? sliderThumb;
        private TimeSpan? lastThumbnailTimeSpan;
        private double? lastPosition;

        public MediaSlider()
        {
            DefaultStyleKey = nameof(MediaSlider);
            mouseCapabilities = new MouseCapabilities();
            SliderToolTipPopupAnchor = this;

            ValueChanged += ProgressSlider_ValueChanged;
            PointerWheelChanged += ProgressSlider_PointerWheelChanged;

            if (mouseCapabilities.MousePresent > 0)
            {
                PointerMoved += ProgressSlider_PointerMoved;
                PointerExited += ProgressSlider_PointerExited;
            }

            if (ApiInformation.IsEventPresent(typeof(UIElement).FullName, nameof(PreviewKeyDown)))
            {
                PreviewKeyDown += ProgressSlider_PreviewKeyDown;
            }
        }

        protected override void OnApplyTemplate()
        {
            mediaDownloadedBar = GetTemplateChild("DownloadProgressIndicator") as ProgressBar;
            sliderRectangle = GetTemplateChild("HorizontalTrackRect") as Rectangle;
            sliderThumb = GetTemplateChild("HorizontalThumb") as Thumb;
            sliderToolTipPopup = Application.Current.Resources["SliderToolTipPopup"] as Popup;
            sliderToolTipTextBlock = sliderToolTipPopup?.Child?.FindVisualChild<TextBlock>("SliderToolTipTextBlock");
            sliderToolTipThumbnailImage =
                sliderToolTipPopup?.Child?.FindVisualChild<Image>("SliderToolTipThumbnailImage");

            if (sliderToolTipThumbnailImage != null)
            {
                sliderToolTipThumbnailImage.SizeChanged += SliderToolTipThumbnailImage_SizeChanged;
            }

            ToolTipService.SetToolTip(this, null);

            if (mouseCapabilities.MousePresent > 0
                && sliderThumb != null)
            {
                sliderThumb.Tapped += SliderThumb_Tapped;
            }

            base.OnApplyTemplate();
        }

        private void SliderToolTipThumbnailImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (lastPosition is not double pos)
            {
                return;
            }

            sliderToolTipPopup!.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var sliderPosition = TransformToVisual(Window.Current.Content).TransformPoint(new Point());
            sliderToolTipPopup.HorizontalOffset =
                sliderPosition.X + pos - (sliderToolTipPopup.Child.DesiredSize.Width / 2);

            var facePosition = SliderToolTipPopupAnchor.TransformToVisual(Window.Current.Content)
                .TransformPoint(new Point());
            sliderToolTipPopup.VerticalOffset = facePosition.Y - sliderToolTipPopup.Child.DesiredSize.Height;
        }

        private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var percents = e.NewValue / Maximum;
            var newPosition = TimeSpan.FromMilliseconds(percents * Duration.TotalMilliseconds);
            PositionChangeRequested?.Invoke(this, new EventArgs<TimeSpan>(newPosition));
        }

        private void SliderThumb_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != PointerDeviceType.Mouse
                || sliderRectangle == null
                || sliderThumb == null)
            {
                return;
            }

            var newPosition = sliderToolTipPopup?.Tag is TimeSpan displayedPosition && sliderToolTipPopup.IsOpen
                ? displayedPosition
                : GetTimeSpanFromSliderPosition(e.GetPosition(sliderRectangle).X);
            PositionChangeRequested?.Invoke(this, new EventArgs<TimeSpan>(newPosition));
        }

        private async void ProgressSlider_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse
                || sliderToolTipTextBlock == null
                || sliderRectangle == null
                || sliderThumb == null
                || e.OriginalSource == sliderThumb)
            {
                return;
            }

            var pos = e.GetCurrentPoint(sliderRectangle).Position.X;
            lastPosition = pos;

            var posTimeSpan = GetTimeSpanFromSliderPosition(pos);

            sliderToolTipPopup!.Tag = posTimeSpan;

            sliderToolTipTextBlock.Text = posTimeSpan.ToFriendlyString(false, Duration.TotalHours >= 1);

            sliderToolTipPopup.IsOpen = true;

            sliderToolTipPopup.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var sliderPosition = TransformToVisual(Window.Current.Content).TransformPoint(new Point());
            sliderToolTipPopup.HorizontalOffset =
                sliderPosition.X + pos - (sliderToolTipPopup.Child.DesiredSize.Width / 2);

            var facePosition = SliderToolTipPopupAnchor.TransformToVisual(Window.Current.Content)
                .TransformPoint(new Point());
            sliderToolTipPopup.VerticalOffset = facePosition.Y - sliderToolTipPopup.Child.DesiredSize.Height;

            if (sliderToolTipThumbnailImage != null
                && ThumbnailRequested != null)
            {
                var lastImageDiffSeconds = lastThumbnailTimeSpan.HasValue
                    ? Math.Abs((lastThumbnailTimeSpan.Value - posTimeSpan).TotalSeconds)
                    : 0;
                if (lastThumbnailTimeSpan.HasValue
                    && lastImageDiffSeconds < 5)
                {
                    return;
                }

                try
                {
                    if (lastImageDiffSeconds > 60)
                    {
                        sliderToolTipThumbnailImage.Source = null;
                    }

                    var deferralManager = new DeferralManager();
                    var size = new BitmapSize {Width = 200};
                    var args = new MediaSliderThumbnailRequestedEventArgs(deferralManager.DeferralSource, posTimeSpan,
                        size, default);
                    ThumbnailRequested?.Invoke(this, args);
                    await deferralManager.WaitForDeferralsAsync().ConfigureAwait(true);

                    if (args.ThumbnailImage is IRandomAccessStream stream)
                    {
                        using (stream)
                        {
                            var source = new BitmapImage();
                            await source.SetSourceAsync(stream);

                            sliderToolTipThumbnailImage.Source = source;
                            if (source != null)
                            {
                                lastThumbnailTimeSpan = posTimeSpan;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning(ex);
                }
            }
        }

        private TimeSpan GetTimeSpanFromSliderPosition(double pos)
        {
            var width = sliderRectangle!.ActualWidth;
            var percent = pos / width;

            var thumbDelta = sliderThumb!.ActualWidth * (percent - 0.5) / width;
            percent = Math.Min(1, Math.Max(0, percent + thumbDelta));

            return TimeSpan.FromMilliseconds(percent * Duration.TotalMilliseconds);
        }

        private void ProgressSlider_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sliderToolTipPopup != null)
            {
                sliderToolTipPopup.IsOpen = false;
            }
        }

        private void ProgressSlider_PointerWheelChanged(object sender, PointerRoutedEventArgs args)
        {
            var properties = args.GetCurrentPoint((UIElement)sender).Properties;
            var wheel = properties.MouseWheelDelta;

            var seekDelta = TimeSpan.FromSeconds(20 * CustomTransportControls.ComputeDeltaFromWheel(wheel));
            SeekRequested?.Invoke(this, new EventArgs<TimeSpan>(seekDelta));
        }

        private void ProgressSlider_PreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            switch (args.Key)
            {
                case VirtualKey.Up:
                case VirtualKey.GamepadDPadUp:
                    args.Handled = FocusManager.TryMoveFocus(FocusNavigationDirection.Up);
                    break;
                case VirtualKey.Down:
                case VirtualKey.GamepadDPadDown:
                    args.Handled = FocusManager.TryMoveFocus(FocusNavigationDirection.Down);
                    break;
            }
        }
    }
}

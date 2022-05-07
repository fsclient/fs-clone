namespace FSClient.UWP.Shared.Views.Controls
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.UWP.Shared.Helpers;

    public class SliderValueChangeCompletedEventArgs : RoutedEventArgs
    {
        public double OldValue { get; }
        public double NewValue { get; }

        public SliderValueChangeCompletedEventArgs(double oldValue, double newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public delegate void SlideValueChangeCompletedEventHandler(object sender, SliderValueChangeCompletedEventArgs args);

    public class ExtendedSlider : Slider
    {
        public event SlideValueChangeCompletedEventHandler? ValueChangeCompleted;
        private double prevValue;
        private bool _dragging;

        protected void OnValueChangeCompleted(double oldValue, double newValue)
        {
            prevValue = newValue;
            if (newValue != oldValue)
            {
                ValueChangeCompleted?.Invoke(this, new SliderValueChangeCompletedEventArgs(oldValue, newValue));
            }
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("HorizontalThumb") is Thumb hThumb)
            {
                hThumb.DragStarted += ThumbOnDragStarted;
                hThumb.DragCompleted += ThumbOnDragCompleted;
            }

            if (GetTemplateChild("VerticalThumb") is Thumb vThumb)
            {
                vThumb.DragStarted += ThumbOnDragStarted;
                vThumb.DragCompleted += ThumbOnDragCompleted;
            }
        }

        private void ThumbOnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            _dragging = false;
            if (this.IsLoaded())
            {
                OnValueChangeCompleted(prevValue, Value);
            }
        }

        private void ThumbOnDragStarted(object sender, DragStartedEventArgs e)
        {
            _dragging = true;
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            base.OnValueChanged(oldValue, newValue);
            if (!_dragging && this.IsLoaded())
            {
                OnValueChangeCompleted(oldValue, newValue);
            }
        }
    }
}

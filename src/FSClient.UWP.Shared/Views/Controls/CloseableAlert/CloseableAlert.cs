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

    public sealed class CloseableAlert : Control
    {
        public CloseableAlert()
        {
            DefaultStyleKey = nameof(CloseableAlert);
        }

        public string? Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(CloseableAlert),
                new PropertyMetadata(null, TextChanged));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(CloseableAlert),
                new PropertyMetadata(false, IsOpenChanged));

        protected override void OnApplyTemplate()
        {
            if (GetTemplateChild("CloseAlertToggleButton") is ToggleButton closeAlertToggleButton)
            {
                closeAlertToggleButton.Unchecked += (_, __) => IsOpen = true;
                closeAlertToggleButton.Checked += (_, __) => IsOpen = false;
                closeAlertToggleButton.IsChecked = !IsOpen;
            }

            if (GetTemplateChild("AlertTextBlock") is TextBlock alertTextBlock)
            {
                alertTextBlock.Text = Text as string ?? string.Empty;
            }

            VisualStateManager.GoToState(this, IsOpen ? "OpenState" : "ClosedState", false);

            base.OnApplyTemplate();
        }

        private static void TextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (((CloseableAlert)d).GetTemplateChild("AlertTextBlock") is TextBlock alertTextBlock)
            {
                alertTextBlock.Text = e.NewValue as string ?? string.Empty;
            }
        }

        private static void IsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var alert = (CloseableAlert)d;
            var newValue = (bool)e.NewValue;
            VisualStateManager.GoToState(alert, newValue ? "OpenState" : "ClosedState", false);

            if (alert.GetTemplateChild("CloseAlertToggleButton") is ToggleButton closeAlertToggleButton)
            {
                closeAlertToggleButton.IsChecked = !newValue;
            }
        }
    }
}

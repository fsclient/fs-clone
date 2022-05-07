namespace FSClient.UWP.Shared.Views.Controls
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    public partial class CustomTransportControls
    {
        public FlyoutBase? MoreFlyout
        {
            get => (FlyoutBase?)GetValue(MoreFlyoutProperty);
            set => SetValue(MoreFlyoutProperty, value);
        }

        public static readonly DependencyProperty MoreFlyoutProperty =
            DependencyProperty.Register(nameof(MoreFlyout), typeof(FlyoutBase), typeof(CustomTransportControls),
                new PropertyMetadata(null));
    }
}

namespace FSClient.UWP.Shared.Views.Controls
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    public class ItemTrailerAppBarButton : AppBarButton
    {
        public ItemTrailerAppBarButton()
        {
            DefaultStyleKey = nameof(ItemTrailerAppBarButton);
        }

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(ItemTrailerAppBarButton),
                new PropertyMetadata(false, IsLoadingChanged));

        private static void IsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var itemTrailerAppBarButton = (ItemTrailerAppBarButton)d;
            if (itemTrailerAppBarButton.GetTemplateChild("TrailerLoadingProgress") is ProgressRing
                trailerLoadingProgress)
            {
                trailerLoadingProgress.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}

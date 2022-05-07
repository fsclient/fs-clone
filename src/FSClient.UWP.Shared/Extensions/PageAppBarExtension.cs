namespace FSClient.UWP.Shared.Extensions
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Data;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Data;
#endif

    public static class PageAppBarExtension
    {
        public static readonly DependencyProperty TopProperty =
            DependencyProperty.RegisterAttached(
                "Top",
                typeof(FrameworkElement),
                typeof(PageAppBarExtension),
                new PropertyMetadata(null, PropertyChangedCallback));

        public static FrameworkElement? GetTop(DependencyObject obj)
        {
            return obj.GetValue(TopProperty) as FrameworkElement;
        }

        public static void SetTop(DependencyObject obj, FrameworkElement? value)
        {
            obj.SetValue(TopProperty, value);
        }

        private static void PropertyChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var appBar = (FrameworkElement?)args.NewValue;
            if (appBar != null
                && appBar.DataContext == null
                && sender is FrameworkElement page)
            {
                appBar.SetBinding(FrameworkElement.DataContextProperty,
                    new Binding
                    {
                        Source = page,
                        Path = new PropertyPath(nameof(FrameworkElement.DataContext)),
                        Mode = BindingMode.OneWay
                    });
            }
        }
    }
}

namespace FSClient.UWP.Shared.Extensions
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    public static class ToolTipServiceEx
    {
        public static readonly DependencyProperty ToolTipProperty =
            DependencyProperty.Register("ToolTip", typeof(string), typeof(ToolTipServiceEx),
                new PropertyMetadata(null, OnToolTipChanged));

        public static string GetToolTip(DependencyObject obj)
        {
            return (string)obj.GetValue(ToolTipProperty);
        }

        public static void SetToolTip(DependencyObject obj, string value)
        {
            obj.SetValue(ToolTipProperty, value);
        }

        private static void OnToolTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is null)
            {
                ToolTipService.SetToolTip(d, null);
            }
            else if ((d as FrameworkElement)?.AccessKey is string accessKey)
            {
                ToolTipService.SetToolTip(d, $"{e.NewValue} (Alt+{accessKey})");
            }
            else
            {
                ToolTipService.SetToolTip(d, e.NewValue);
            }
        }
    }
}

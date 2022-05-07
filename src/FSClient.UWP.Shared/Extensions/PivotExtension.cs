namespace FSClient.UWP.Shared.Extensions
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.UWP.Shared.Helpers;

    public static class PivotExtension
    {
        public static readonly DependencyProperty HeaderVisibilityProperty =
            DependencyProperty.Register("HeaderVisibility", typeof(Visibility), typeof(PivotExtension),
                new PropertyMetadata(Visibility.Visible, OnHeaderVisibilityChanged));

        public static Visibility GetHeaderVisibility(DependencyObject dependencyObject)
        {
            return dependencyObject.GetValue(HeaderVisibilityProperty) as Visibility? ?? Visibility.Visible;
        }

        public static void SetHeaderVisibility(DependencyObject dependencyObject, Visibility visibility)
        {
            dependencyObject.SetValue(HeaderVisibilityProperty, visibility);
        }

        private static void OnHeaderVisibilityChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is Pivot pivot
                && args.NewValue is Visibility visibility)
            {
                if (pivot.FindVisualChild("PivotLayoutElement") is Grid pivotGrid)
                {
                    pivotGrid.SetHeaderRowVisibility(visibility);
                }
                else
                {
                    pivot.Loaded += Pivot_Loaded;
                }
            }
        }

        private static void Pivot_Loaded(object sender, RoutedEventArgs e)
        {
            var pivot = (Pivot)sender;
            pivot.Loaded -= Pivot_Loaded;

            if (GetHeaderVisibility(pivot) is Visibility visibility
                && pivot.FindVisualChild("PivotLayoutElement") is Grid pivotGrid)
            {
                pivotGrid.SetHeaderRowVisibility(visibility);
            }
        }

        private static void SetHeaderRowVisibility(this Grid grid, Visibility newValue)
        {
            foreach (var child in grid.Children)
            {
                var row = child.GetValue(Grid.RowProperty) as int?;
                if (!row.HasValue
                    || row == 0)
                {
                    child.Visibility = newValue;
                }
            }
        }
    }
}

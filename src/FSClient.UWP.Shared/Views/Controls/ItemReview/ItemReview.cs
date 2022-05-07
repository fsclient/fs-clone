namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Linq;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.UWP.Shared.Helpers;
    using FSClient.Localization.Resources;
    using FSClient.ViewModels.Items;

    public class ItemReview : Control
    {
        public ItemReview()
        {
            DefaultStyleKey = nameof(ItemReview);
        }

        public ReviewListItemViewModel Review
        {
            get => (ReviewListItemViewModel)GetValue(ReviewProperty);
            set => SetValue(ReviewProperty, value);
        }

        public static readonly DependencyProperty ReviewProperty =
            DependencyProperty.Register(nameof(Review), typeof(ReviewListItemViewModel), typeof(ItemReview),
                new PropertyMetadata(null));

        protected override void OnApplyTemplate()
        {
            if (GetTemplateChild("ShowHideReview") is HyperlinkButton showHideReview)
            {
                showHideReview.Click += ShowHideReview_Click;
            }

            base.OnApplyTemplate();
        }

        private void ShowHideReview_Click(object sender, object e)
        {
            var hyperlink = (HyperlinkButton)sender;
            hyperlink.Visibility = Visibility.Visible;
            var parent = hyperlink.FindVisualAscendant<Grid>("ReviewGrid");
            var scrollViewer = hyperlink.FindVisualAscendant<ScrollViewer>();
            if (parent != null && scrollViewer != null)
            {
                if (double.IsNaN(parent.Height))
                {
                    ReviewClose(scrollViewer, null);
                }
                else
                {
                    parent.Height = double.NaN;
                    hyperlink.Content = Strings.ItemReview_ShowHideReview_Collaps;
                    scrollViewer.PointerCanceled += ReviewClose;
                    scrollViewer.PointerCaptureLost += ReviewClose;
                    scrollViewer.PointerExited += ReviewClose;
                    scrollViewer.LostFocus += ReviewClose;
                }
            }
        }

        private void ReviewClose(object sender, RoutedEventArgs? e)
        {
            if ((ScrollViewer)sender == (e?.OriginalSource as FrameworkElement)?.FindVisualAscendant<ScrollViewer>())
            {
                return;
            }

            if (sender is ScrollViewer scrollViewer
                && scrollViewer.Content is FrameworkElement child)
            {
                child.Height = 200;
                child.FindVisualChildren<HyperlinkButton>().First().Content = Strings.ItemReview_ShowHideReview_Open;
                scrollViewer.PointerCanceled -= ReviewClose;
                scrollViewer.PointerCaptureLost -= ReviewClose;
                scrollViewer.PointerExited -= ReviewClose;
                scrollViewer.LostFocus -= ReviewClose;
            }
        }
    }
}

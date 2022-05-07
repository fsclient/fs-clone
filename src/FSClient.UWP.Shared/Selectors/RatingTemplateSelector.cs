namespace FSClient.UWP.Shared.Selectors
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Providers;

    public class RatingTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? UpDownRatingTemplate { get; set; }
        public DataTemplate? NumberBasedRatingTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(
            object item,
            DependencyObject container)
        {
            return item switch
            {
                UpDownRating _ when UpDownRatingTemplate != null => UpDownRatingTemplate,
                NumberBasedRating _ when NumberBasedRatingTemplate != null => NumberBasedRatingTemplate,

                _ => base.SelectTemplateCore(item, container),
            };
        }
    }
}

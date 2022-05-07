namespace FSClient.UWP.Shared.Selectors
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.ViewModels.Pages;

    public class PageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? HomePageTemplate { get; set; }
        public DataTemplate? SectionPageTemplate { get; set; }
        public DataTemplate? SearchPageTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item switch
            {
                HomePageViewModel _ when HomePageTemplate != null => HomePageTemplate,
                SearchPageViewModel _ when SearchPageTemplate != null => SearchPageTemplate,
                SectionPageViewModel _ when SectionPageTemplate != null => SectionPageTemplate,
                _ => base.SelectTemplateCore(item, container),
            };
        }
    }
}

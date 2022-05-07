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

    public class SettingsTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? MainSettingsTemplate { get; set; }
        public DataTemplate? OnlineSettingsTemplate { get; set; }
        public DataTemplate? DownloadSettingsTemplate { get; set; }
        public DataTemplate? DataSettingsTemplate { get; set; }
        public DataTemplate? ProvidersSettingsTemplate { get; set; }
        public DataTemplate? AppSettingsTemplate { get; set; }
        public DataTemplate? AboutSettingsTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(
            object item,
            DependencyObject container)
        {
            return ((item as SettingPageViewModel)?.SettingType) switch
            {
                SettingType.Main when MainSettingsTemplate != null => MainSettingsTemplate,
                SettingType.Online when OnlineSettingsTemplate != null => OnlineSettingsTemplate,
                SettingType.Download when DownloadSettingsTemplate != null => DownloadSettingsTemplate,
                SettingType.Data when DataSettingsTemplate != null => DataSettingsTemplate,
                SettingType.Provider when ProvidersSettingsTemplate != null => ProvidersSettingsTemplate,
                SettingType.App when AppSettingsTemplate != null => AppSettingsTemplate,
                SettingType.About when AboutSettingsTemplate != null => AboutSettingsTemplate,

                _ => base.SelectTemplateCore(item, container),
            };
        }
    }
}

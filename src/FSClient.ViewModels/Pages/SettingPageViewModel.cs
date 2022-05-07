namespace FSClient.ViewModels.Pages
{
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;

    public enum SettingType
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SettingsType_Main))]
        Main = 1,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SettingsType_Online))]
        Online,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SettingsType_Download))]
        Download,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SettingsType_Provider))]
        Provider,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SettingsType_Data))]
        Data,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SettingsType_App))]
        App,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SettingsType_About))]
        About
    }

    public class SettingPageViewModel
    {
        public string Header => SettingType.GetDisplayName()!;
        public SettingType SettingType { get; set; }
        public SettingViewModel? ViewModel { get; set; }
    }
}

namespace FSClient.Shared.Services
{
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;

    public enum NavigationPageType
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_Home))]
        Home = 0,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_Favorites))]
        Favorites = 1,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_History))]
        History = 2,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_LastWatched))]
        LastWatched = 3,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_Search))]
        Search = 4,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_Downloads))]
        Downloads = 5,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_Files))]
        Files = 6,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_ItemInfo))]
        ItemInfo = 7,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_ItemsByTag))]
        ItemsByTag = 8,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_Video))]
        Video = 9,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NavigationPageType_Settings))]
        Settings = 10,
    }
}

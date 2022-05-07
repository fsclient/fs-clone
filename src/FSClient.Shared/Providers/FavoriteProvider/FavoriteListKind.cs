namespace FSClient.Shared.Providers
{
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;

    public enum FavoriteListKind
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FavoriteListKind_None))]
        None = 0,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FavoriteListKind_Favorites))]
        Favorites,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FavoriteListKind_ForLater))]
        ForLater,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FavoriteListKind_InProcess))]
        InProcess,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FavoriteListKind_Finished))]
        Finished
    }
}

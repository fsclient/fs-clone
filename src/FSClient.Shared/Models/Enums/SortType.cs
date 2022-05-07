namespace FSClient.Shared.Models
{
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;

    public enum SortType
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_Trend))]
        Trend,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_UpdateDate))]
        UpdateDate,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_Rating))]
        Rating,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_Year))]
        Year,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_Popularity))]
        Popularity,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_Alphabet))]
        Alphabet,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_Commented))]
        Commented,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_Visit))]
        Visit,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_LastViewed))]
        LastViewed,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_Random))]
        Random,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_Episodes))]
        Episodes,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_IMDb))]
        IMDb,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_KinoPoisk))]
        KinoPoisk,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_Revenue))]
        Revenue,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_CreateDate))]
        CreateDate,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SortType_FileSize))]
        FileSize
    }
}

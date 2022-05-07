namespace FSClient.Shared.Services
{
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;

    public enum CheckForUpdatesResultType
    {
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CheckForUpdatesResult_NoUpdates))]
        NoUpdates,

        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CheckForUpdatesResult_Skipped))]
        Skipped,

        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CheckForUpdatesResult_Available))]
        Available,

        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CheckForUpdatesResult_NotSupported))]
        NotSupported,

        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CheckForUpdatesResult_UnknownError))]
        UnknownError
    }
}

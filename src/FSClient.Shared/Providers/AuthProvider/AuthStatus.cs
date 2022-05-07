namespace FSClient.Shared.Providers
{
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;

    public enum AuthStatus
    {
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.AuthStatus_Error))]
        Error = 1,

        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.AuthStatus_Success))]
        Success,

        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.AuthStatus_Canceled))]
        Canceled
    }
}

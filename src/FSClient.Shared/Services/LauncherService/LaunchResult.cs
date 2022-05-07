namespace FSClient.Shared.Services
{
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;

    public enum LaunchResult
    {
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.LaunchResult_Success))]
        Success,

        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.LaunchResult_ErrorInvalidStorageItem))]
        ErrorInvalidStorageItem,

        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.LaunchResult_ErrorHandlerIsNotAvailable))]
        ErrorHandlerIsNotAvailable,

        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.LaunchResult_UnknownError))]
        UnknownError
    }
}

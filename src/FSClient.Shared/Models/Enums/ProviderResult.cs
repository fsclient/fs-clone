namespace FSClient.Shared.Models
{
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;

    /// <summary>
    /// Provider response or failure result.
    /// </summary>
    public enum ProviderResult
    {
        /// <summary>
        /// Unknown result.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_Unknown))]
        Unknown = 0,

        /// <summary>
        /// Success result.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_Success))]
        Success = 1,

        /// <summary>
        /// Unknown failure.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_Failed))]
        Failed,

        /// <summary>
        /// Provider is not available.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_NotAvailable))]
        NotAvailable,

        /// <summary>
        /// Item was not found on provider.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_NotFound))]
        NotFound,

        /// <summary>
        /// Operation is no supported by provider.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_NotSupported))]
        NotSupported,

        /// <summary>
        /// Provider requires user to be logged in.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_NeedLogin))]
        NeedLogin,

        /// <summary>
        /// Provider requires user to be logged in with PRO account.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_NeedProAccount))]
        NeedProAccount,

        /// <summary>
        /// Operation was cancelled.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_Canceled))]
        Canceled,

        /// <summary>
        /// Item or provider was blocked.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_Blocked))]
        Blocked,

        /// <summary>
        /// No valid provider was found.
        /// </summary>
        [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ProviderResult_NoValidProvider))]
        NoValidProvider
    }
}

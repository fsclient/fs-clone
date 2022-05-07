namespace FSClient.Shared.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;

    [Flags]
    public enum ClearCacheModes
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClearCacheModes_None))]
        None = 0,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClearCacheModes_OnStart))]
        OnStart = 1,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClearCacheModes_OnExit))]
        OnExit = 2,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClearCacheModes_OnTimer))]
        OnTimer = 4,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClearCacheModes_OnStartAndExit))]
        OnStartAndExit = OnStart | OnExit
    }
}

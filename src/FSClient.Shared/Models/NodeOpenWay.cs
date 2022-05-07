namespace FSClient.Shared.Models
{
    using System.ComponentModel.DataAnnotations;

    using FSClient.Localization.Resources;

    public enum NodeOpenWay
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NodeOpenWay_InApp))]
        InApp = 0,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NodeOpenWay_InBrowser))]
        InBrowser = 1,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NodeOpenWay_InSeparatedWindow))]
        InSeparatedWindow = 2,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NodeOpenWay_Remote))]
        Remote = 3,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NodeOpenWay_In3rdPartyApp), Description = nameof(Strings.NodeOpenWay_In3rdPartyApp_Description))]
        In3rdPartyApp = 4,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NodeOpenWay_CopyLink))]
        CopyLink = 5
    }
}

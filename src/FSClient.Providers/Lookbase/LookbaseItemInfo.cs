namespace FSClient.Providers
{
    using FSClient.Shared.Models;

    public class LookbaseItemInfo : ItemInfo
    {
        public LookbaseItemInfo(Site site, string? id)
            : base(site, id)
        {
        }

        public string? Translation { get; set; }
    }
}

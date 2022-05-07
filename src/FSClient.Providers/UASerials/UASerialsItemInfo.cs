namespace FSClient.Providers
{
    using FSClient.Shared.Models;

    public class UASerialsItemInfo : ItemInfo
    {
        public UASerialsItemInfo(Site site, string id)
            : base(site, id)
        {

        }

        public string? Translation { get; set; }

        public string? DataTag { get; set; }
    }
}

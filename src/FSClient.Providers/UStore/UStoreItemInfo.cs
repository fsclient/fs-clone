namespace FSClient.Providers
{
    using System.Collections.Generic;

    using FSClient.Shared.Models;

    public class UStoreItemInfo : ItemInfo
    {
        public UStoreItemInfo(Site site, string? id) : base(site, id)
        {
            EpisodesPerSeasonsPerTranslation = new Dictionary<string, IReadOnlyDictionary<int, IReadOnlyCollection<string>>>();
        }

        public IReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyCollection<string>>> EpisodesPerSeasonsPerTranslation { get; set; }
    }
}

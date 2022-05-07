namespace FSClient.Providers
{
    using System.Collections.Generic;

    using FSClient.Shared.Models;

    public class HDVBItemInfo : ItemInfo
    {
        public HDVBItemInfo(Site site, string? id) : base(site, id)
        {
            EpisodesPerSeasons = new Dictionary<int, IReadOnlyCollection<int>>();
        }

        public string? Translate { get; set; }

        public IReadOnlyDictionary<int, IReadOnlyCollection<int>> EpisodesPerSeasons { get; set; }
    }
}

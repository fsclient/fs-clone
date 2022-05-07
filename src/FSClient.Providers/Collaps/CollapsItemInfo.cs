namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Models;

    public class CollapsItemInfo : ItemInfo
    {
        public CollapsItemInfo(Site site, string? id) : base(site, id)
        {
            EpisodesPerSeasons = new Dictionary<int, IReadOnlyCollection<(int, Uri)>>();
        }

        public IReadOnlyDictionary<int, IReadOnlyCollection<(int episode, Uri link)>> EpisodesPerSeasons { get; set; }
    }
}

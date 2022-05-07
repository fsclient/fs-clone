namespace FSClient.Providers
{
    using System.Collections.Generic;

    using FSClient.Shared.Models;

    public class BazonItemInfo : ItemInfo
    {
        public BazonItemInfo(Site site, string? id) : base(site, id)
        {
            EpisodesPerSeasons = new Dictionary<int, IReadOnlyCollection<(int episode, string quality)>>();
        }

        public string? Translation { get; set; }

        public int? TranslationId { get; set; }

        public IReadOnlyDictionary<int, IReadOnlyCollection<(int episode, string quality)>> EpisodesPerSeasons { get; set; }
    }
}

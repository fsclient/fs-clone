namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class CollapsItemInfoProvider : IItemInfoProvider
    {
        private readonly CollapsSiteProvider siteProvider;

        public CollapsItemInfoProvider(CollapsSiteProvider collapsSiteProvider)
        {
            siteProvider = collapsSiteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return false;
        }

        public Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            return Task.FromResult<ItemInfo?>(null);
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            return Task.FromResult(item.Site == Site);
        }

        internal static CollapsItemInfo GetItemFromJObject(Site site, Uri domain, JObject item)
        {
            var titleRu = item["name"]?.ToString();
            var titleEn = item["origin_name"]?.ToString();
            var kinopoiskId = item["kinopoisk_id"]?.ToIntOrNull();
            var playerLink = item["iframe_url"]?.ToUriOrNull(domain);
            var isSerial = item["seasons"] != null;
            var seasonsCount = (item["seasons"] as JArray)?.Count;
            var year = item["year"]?.ToIntOrNull();
            var poster = item["poster"]?.ToUriOrNull(domain);

            if (year == 0)
            {
                year = null;
            }

            var collapsId = item["id"];
            var itemId = collapsId == null
                ? $"kp{kinopoiskId}"
                : $"clps{collapsId}";

            var itemInfo = new CollapsItemInfo(site, itemId)
            {
                Title = titleRu,
                Link = playerLink,
                Poster = poster,
                Details =
                {
                    Year = year,
                    TitleOrigin = titleEn,
                    Status = new Status(currentSeason: seasonsCount)
                },
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
            };
            if (kinopoiskId.HasValue)
            {
                itemInfo.Details.LinkedIds.Add(Sites.Kinopoisk, kinopoiskId.ToString());
            }

            if (item["seasons"] is JArray seasons)
            {
                itemInfo.EpisodesPerSeasons = seasons
                    .Select((season, index) =>
                    {
                        var seasonNumber = season["season"]?.ToIntOrNull() ?? index + 1;
                        var episodes = (season["episodes"] as JArray)?
                            .Select((t, i) => (
                                episode: t["episode"]?.ToIntOrNull() ?? (i + 1),
                                link: t["iframe_url"]?.ToUriOrNull()
                            ))
                            .Where(t => t.link != null)
                            ?? Enumerable.Empty<(int, Uri)>();

                        return (seasonNumber, episodes: episodes.ToList());
                    })
                    .Where(t => t.episodes.Any())
                    .ToDictionary(
                        t => t.seasonNumber,
                        t => (IReadOnlyCollection<(int, Uri)>)t.episodes.AsReadOnly());
            }

            return itemInfo;
        }
    }
}

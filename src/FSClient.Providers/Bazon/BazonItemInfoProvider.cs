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

    public class BazonItemInfoProvider : IItemInfoProvider
    {
        private readonly BazonSiteProvider siteProvider;

        public BazonItemInfoProvider(BazonSiteProvider bazonSiteProvider)
        {
            siteProvider = bazonSiteProvider;
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

        internal static BazonItemInfo? GetItemFromJObject(Site site, Uri domain, JObject item, bool skipBlocked = true)
        {
            if (skipBlocked
                && item["block"]?.ToString() == "1")
            {
                return null;
            }

            var info = item["info"] as JObject;

            var titleRu = info?["rus"]?.ToString();
            var titleEn = info?["orig"]?.ToString();
            var kinopoiskId = item["kinopoisk_id"]?.ToIntOrNull();
            var playerLink = item["link"]?.ToUriOrNull(domain);
            var isSerial = item["serial"]?.ToString() == "1";
            var seasonsCount = item["last_season"]?.ToIntOrNull();
            var year = info?["year"]?.ToIntOrNull();
            var poster = info?["poster"]?.ToUriOrNull(domain);
            var translation = item["translation"]?.ToString();
            var topQuality = item["max_qual"]?.ToString();

            if (year == 0
                || !kinopoiskId.HasValue)
            {
                year = null;
            }

            if (playerLink == null)
            {
                return null;
            }

            var itemInfo = new BazonItemInfo(site, kinopoiskId.ToString())
            {
                Title = titleRu,
                Link = playerLink,
                Poster = poster,
                Translation = translation,
                TranslationId = playerLink.Segments.Skip(3).FirstOrDefault()?.Trim('/').ToIntOrNull(),
                Details =
                {
                    Quality = topQuality,
                    Year = year,
                    TitleOrigin = titleEn,
                    Status = new Status(currentSeason: seasonsCount),
                    LinkedIds =
                    {
                        [Sites.Kinopoisk] = kinopoiskId.ToString()
                    }
                },
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
            };

            if (item["episodes"] is JObject seasons)
            {
                itemInfo.EpisodesPerSeasons = seasons
                    .Properties()
                    .Select((season, seasonIndex) =>
                    {
                        var seasonNumber = season.Name.ToIntOrNull() ?? seasonIndex + 1;
                        var episodes = (season.Value as JObject)?
                            .Properties()
                            .Select((episodeProp, episodeIndex) => (
                                episode: episodeProp.Name.ToIntOrNull() ?? episodeIndex + 1,
                                quality: episodeProp.Value.ToString()
                            ))
                            ?? Enumerable.Empty<(int, string)>();

                        return (seasonNumber, episodes: episodes.ToList());
                    })
                    .Where(t => t.episodes.Any())
                    .ToDictionary(
                        t => t.seasonNumber,
                        t => (IReadOnlyCollection<(int, string)>)t.episodes.AsReadOnly());
            }

            return itemInfo;
        }
    }
}

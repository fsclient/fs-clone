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

    public class UStoreItemInfoProvider : IItemInfoProvider
    {
        private readonly UStoreSiteProvider siteProvider;

        public UStoreItemInfoProvider(UStoreSiteProvider uStoreSiteProvider)
        {
            siteProvider = uStoreSiteProvider;
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

        internal static UStoreItemInfo? GetItemFromJObject(Site site, Uri domain, JObject item)
        {
            var title = item["title"]?.ToString().Split('/', '\\') ?? Array.Empty<string>();
            var year = item["year"]?.ToString().Split('-') ?? Array.Empty<string>();
            var titleRu = title.FirstOrDefault()?.Trim();
            var titleEn = title.Skip(1).FirstOrDefault()?.Trim();
            var kinopoiskId = item["kinopoisk_id"]?.ToIntOrNull();
            var imdbId = item["imdb_id"]?.ToString();
            var playerLink = item["iframe"]?.ToUriOrNull(domain);
            var contentId = item["contentId"]?.ToString();
            var isSerial = item["playlist"] != null;
            var yearStart = year.FirstOrDefault()?.ToIntOrNull();
            var yearEnd = year.Skip(1).FirstOrDefault()?.ToIntOrNull();
            var translation = item["translate"]?.ToString();
            var topQuality = item["quality"]?.ToString();

            if (yearStart == 0
                || !kinopoiskId.HasValue)
            {
                yearStart = null;
            }

            if (playerLink == null)
            {
                return null;
            }

            var itemInfo = new UStoreItemInfo(site, kinopoiskId.ToString())
            {
                Title = titleRu,
                Link = playerLink,
                Details =
                {
                    Quality = topQuality,
                    Year = yearStart,
                    YearEnd = yearEnd,
                    TitleOrigin = titleEn,
                    LinkedIds =
                    {
                        [Sites.Kinopoisk] = kinopoiskId.ToString()
                    }
                },
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
            };

            if (imdbId != null)
            {
                itemInfo.Details.LinkedIds.Add(Sites.IMDb, imdbId);
            }

            if (item["playlist"] is JArray playlist)
            {
                itemInfo.EpisodesPerSeasonsPerTranslation = playlist
                    .OfType<JObject>()
                    .Select(translateObject => (
                        translate: translateObject["translate"]?.ToString() ?? string.Empty,
                        data: translateObject["data"] as JObject))
                    .GroupBy(t => t.translate)
                    .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<int, IReadOnlyCollection<string>>)g
                        .SelectMany(t => (t.data ?? new JObject()).Properties())
                        .Select(seasonObject => (
                            seasonNumber: seasonObject.Name.ToIntOrNull() ?? 0,
                            episodes: (IReadOnlyCollection<string>)(seasonObject.Value as JArray ?? new JArray())
                                .Select(ep => ep.ToString())
                                .ToList()
                                .AsReadOnly()
                        ))
                        .GroupBy(t => t.seasonNumber)
                        .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.episodes.Count).First().episodes));
            }
            else if ((contentId ?? playerLink.Segments.Skip(2).FirstOrDefault()?.TrimStart('/')) is string epHash)
            {
                itemInfo.EpisodesPerSeasonsPerTranslation = new Dictionary<string, IReadOnlyDictionary<int, IReadOnlyCollection<string>>>
                {
                    [translation ?? titleRu ?? string.Empty] = new Dictionary<int, IReadOnlyCollection<string>>
                    {
                        [0] = new[] { epHash }
                    }
                };
            }

            return itemInfo;
        }
    }
}

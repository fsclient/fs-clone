namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class HDVBItemInfoProvider : IItemInfoProvider
    {
        private readonly HDVBSiteProvider siteProvider;

        public HDVBItemInfoProvider(HDVBSiteProvider hdvbSiteProvider)
        {
            siteProvider = hdvbSiteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return link != null && (link.Segments.Contains("serial/") || link.Segments.Contains("movie/"))
                && link.Segments.Length > 2;
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            var segments = link?.GetPath().Split('/').Where(p => !string.IsNullOrEmpty(p));
            var id = segments?.Skip(1).FirstOrDefault();
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var section = segments?.FirstOrDefault();
            var isSerial = section == "serial";

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var itemJson = await siteProvider.HttpClient
                .GetBuilder(new Uri(domain, $"/api/{(isSerial ? "serial" : "movie")}.json"))
                .WithArgument("video_token", id)
                .WithArgument("token", Secrets.HDVBApiKey)
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            if (itemJson == null)
            {
                return null;
            }

            return GetItemFromJObject(Site, domain, itemJson);
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            return Task.FromResult(item.Site == Site);
        }

        internal static HDVBItemInfo GetItemFromJObject(Site site, Uri domain, JObject item)
        {
            var titleRu = item["title_ru"]?.ToString();
            var titleEn = item["title_en"]?.ToString();
            var kinopoiskId = item["kinopoisk_id"]?.ToIntOrNull();
            var playerLink = item["iframe_url"]?.ToUriOrNull(domain);
            var translate = item["translator"]?.ToString();
            var isSerial = item["type"]?.ToString() == "serial";
            var seasonsCount = (item["serial_episodes"] as JArray)?.Count;
            var year = item["year"]?.ToIntOrNull();
            var poster = item["poster"]?.ToUriOrNull(domain);
            var quality = item["quality"]?.ToString();

            if (year == 0)
            {
                year = null;
            }

            var hdvbId = item["token"];
            var itemId = hdvbId == null
                ? $"kp{kinopoiskId}"
                : $"hd{hdvbId}";

            var itemInfo = new HDVBItemInfo(site, itemId)
            {
                Title = titleRu,
                Link = playerLink,
                Translate = translate,
                Poster = poster,
                Details =
                {
                    Quality = quality,
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

            if (item["serial_episodes"] is JArray seasons)
            {
                itemInfo.EpisodesPerSeasons = seasons
                    .Select((season, index) =>
                    {
                        var seasonNumber = season["season_number"]?.ToIntOrNull() ?? index + 1;
                        var episodesCount = season["episodes_count"]?.ToIntOrNull() ?? 0;
                        var episodes = (season["episodes"] as JArray)?
                            .Select(t => t.ToIntOrNull() ?? -1)
                            .Distinct()
                            .Where(episode => episode > 0) ?? Enumerable.Range(0, episodesCount);

                        return (seasonNumber, episodes: episodes.ToList());
                    })
                    .Where(t => t.episodes.Any())
                    .ToDictionary(
                        t => t.seasonNumber,
                        t => (IReadOnlyCollection<int>)t.episodes.AsReadOnly());
            }

            return itemInfo;
        }
    }
}

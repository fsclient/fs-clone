namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class VideoCDNItemInfoProvider : IItemInfoProvider
    {
        private readonly VideoCDNSiteProvider siteProvider;

        public VideoCDNItemInfoProvider(VideoCDNSiteProvider videoCDNSiteProvider)
        {
            siteProvider = videoCDNSiteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return link != null && (link.Segments.Contains("movie/") || link.Segments.Contains("tv-series/"))
                && link.Segments.Length > 3;
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            var segments = link?.GetPath().Split('/').Where(p => !string.IsNullOrEmpty(p));
            var id = segments?.LastOrDefault();
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var itemJson = await siteProvider.HttpClient
                .GetBuilder(new Uri(domain, $"/api/short"))
                .WithArgument("id", id)
                .WithArgument("api_token", Secrets.VideoCDNApiKey)
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            itemJson = (itemJson?["data"] as JArray)?.FirstOrDefault() as JObject;
            if (itemJson == null)
            {
                return null;
            }

            return GetItemFromJObject(siteProvider, domain, itemJson);
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            return Task.FromResult(item.Site == Site);
        }

        internal static ItemInfo? GetItemFromJObject(VideoCDNSiteProvider siteProvider, Uri domain, JObject jObject)
        {
            var item = new ItemInfo(siteProvider.Site, jObject["id"]?.ToString())
            {
                Title = (jObject["ru_title"] ?? jObject["title"])?.ToString(),
                Link = jObject["iframe_src"]?.ToUriOrNull(domain),
                Details =
                {
                    Quality = jObject["quality"]?.ToString(),
                    TitleOrigin = jObject["orig_title"]?.ToString(),
                }
            };
            if (item.Link == null || item.SiteId == null)
            {
                return null;
            }

            var path = item.Link.GetPath();
            if (path != null)
            {
                item.Section = siteProvider.Sections
                    .Where(s => s != default)
                    // remove 's' from 'movies' and same with other
                    .FirstOrDefault(s => path.Contains(s.Value.TrimEnd('s')));
            }
            if (item.Section == Section.Any
                && jObject["type"]?.ToString() is string type)
            {
                var sectionModifier = SectionModifiers.None
                    | (type == "movie" ? SectionModifiers.Film : SectionModifiers.None)
                    | (type == "serial" ? SectionModifiers.Serial : SectionModifiers.None);
                if (sectionModifier != SectionModifiers.None)
                {
                    item.Section = Section.CreateDefault(sectionModifier);
                }
            }

            if (jObject["seasons_count"]?.ToIntOrNull() is int seasonsCount)
            {
                item.Details.Status = new Status(currentSeason: seasonsCount);
            }
            if ((jObject["kp_id"] ?? jObject["kinopoisk_id"])?.ToIntOrNull() is int kpId)
            {
                item.Details.LinkedIds.Add(Sites.Kinopoisk, kpId.ToString());
            }
            if (jObject["imdb_id"]?.ToString() is string imdbId)
            {
                item.Details.LinkedIds.Add(Sites.IMDb, imdbId);
            }
            if (jObject["world_art_id"]?.ToString() is string worldArtId)
            {
                item.Details.LinkedIds.Add(Sites.WorldArt, worldArtId);
            }
            return item;


        }
    }
}

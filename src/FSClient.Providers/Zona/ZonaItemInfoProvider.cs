namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class ZonaItemInfoProvider : IItemInfoProvider
    {
        private readonly ZonaSiteProvider siteProvider;

        public ZonaItemInfoProvider(ZonaSiteProvider zonaSiteProvider)
        {
            siteProvider = zonaSiteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return link?.Host.Contains("zona") == true;
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            if (link == null)
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            link = new Uri(domain, "/api/v1/" + link.GetPath());
            var json = await siteProvider
                .HttpClient
                .GetBuilder(link)
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            if (json == null
                || json["mobi_link_id"]?.ToString() is not string id
                || json["name_id"]?.ToString() is not string nameId)
            {
                return null;
            }

            var isSerial = json["serial"]?.ToBoolOrNull() ?? false;
            var isCartoon = (json["genres"] as JArray ?? new JArray())
                .Any(g => g["translit"]?.ToString() == "multfilm" || g["translit"]?.ToString() == "anime");

            var item = new ItemInfo(Site, id)
            {
                Link = new Uri(domain, (isSerial ? "/tvseries/" : "/movies/") + nameId),
                Title = json["name_rus"]?.ToString().Trim(),
                Poster = json["image"]?.ToUriOrNull(),
                Section = Section.CreateDefault(
                    (isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
                    | (isCartoon ? SectionModifiers.Cartoon : SectionModifiers.None)),
                Details =
                {
                    TitleOrigin = json["name_original"]?.ToString().Trim(),
                    Description = json["description"]?.ToString().Trim(),
                    Year = json["release_date"]?["year"]?.ToIntOrNull()
                }
            };
            if (json["id"]?.ToString() is string kpId)
            {
                item.Details.LinkedIds.Add(Sites.Kinopoisk, kpId);
            }
            return item;
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

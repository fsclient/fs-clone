namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class NekomoriSearchProvider : ISearchProvider
    {
        private readonly NekomoriSiteProvider siteProvider;
        private readonly ShikiSearchProvider shikiSearchProvider;

        public NekomoriSearchProvider(
            NekomoriSiteProvider siteProvider,
            ShikiSearchProvider shikiSearchProvider)
        {
            this.siteProvider = siteProvider;
            this.shikiSearchProvider = shikiSearchProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>();

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return new[] { original };
            }

            if (original.Details.TitleOrigin == null
                || !original.Section.Modifier.HasFlag(SectionModifiers.Cartoon))
            {
                return Enumerable.Empty<ItemInfo>();
            }

            var shikiItem = original.Site == ShikiSiteProvider.SiteKey ? original : (await shikiSearchProvider
                .FindSimilarAsync(original, cancellationToken)
                .ConfigureAwait(false))
                .FirstOrDefault();

            if (shikiItem == null
                || shikiItem.Site != ShikiSiteProvider.SiteKey
                || shikiItem.SiteId?.GetDigits().ToIntOrNull() is not int shikiId)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var directItemText = await siteProvider.HttpClient
                .GetBuilder(new Uri(domain, $"/api/arts/-{shikiId}"))
                .WithHeader("Referer", domain.ToString())
                .WithHeader("Origin", domain.GetOrigin())
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false) ?? "{}";

            if (directItemText != "Запись не существует"
                && JsonHelper.ParseOrNull<JObject>(directItemText) is JObject directItem
                && ParseItemFromJsonObject(Site, directItem).itemInfo is ItemInfo directItemInfo)
            {
                directItemInfo.Link = await GetLink(directItemInfo.SiteId!).ConfigureAwait(false);
                return new[] { directItemInfo };
            }

            var jsonItems = await siteProvider.HttpClient
                .GetBuilder(new Uri(domain, "/api/arts"))
                .WithArgument("search", shikiItem.Details.TitleOrigin ?? shikiItem.Title)
                .WithArgument("kind", "anime")
                .WithArgument("ep", "all")
                .WithArgument("take", "50")
                .WithHeader("Referer", domain.ToString())
                .WithHeader("Origin", domain.GetOrigin())
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JArray>()
                .ConfigureAwait(false) ?? new JArray();

            var itemInfo = jsonItems.OfType<JObject>()
                .Select(jsonObject => ParseItemFromJsonObject(Site, jsonObject))
                .FirstOrDefault(t => t.malId == shikiId)
                .itemInfo;

            if (itemInfo == null)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            itemInfo.Link = await GetLink(itemInfo.SiteId!).ConfigureAwait(false);

            return new[] { itemInfo };

            static (int? malId, ItemInfo? itemInfo) ParseItemFromJsonObject(Site site, JObject json)
            {
                var nekomoriId = json["id"]?.ToIntOrNull();
                var malId = json["externalId"]?["malId"]?.ToIntOrNull();
                if (nekomoriId == null)
                {
                    return default;
                }

                var status = new Status(
                    currentEpisode: json["episodes"]?["aired"]?.ToIntOrNull(),
                    totalEpisodes: json["episodes"]?["total"]?.ToIntOrNull());

                var itemInfo = new ItemInfo(site, nekomoriId.ToString())
                {
                    Title = json["name"]?["ru"]?.ToString(),
                    Section = Section.CreateDefault(SectionModifiers.Anime
                        | (status.TotalEpisodes > 1 || status.CurrentEpisode > 1
                            ? SectionModifiers.Serial
                            : SectionModifiers.Film)),
                    Details =
                    {
                        TitleOrigin = json["name"]?["rj"]?.ToString(),
                        Status = status
                    }
                };

                return (malId, itemInfo);
            }

            async Task<Uri?> GetLink(string id)
            {
                var response = await siteProvider.HttpClient
                    .GetBuilder(new Uri(domain, "/api/external/kartinka"))
                    .WithArgument("artId", id)
                    .WithAjax()
                    .WithHeader("Referer", new Uri(domain, $"/anime/{id}/general").ToString())
                    .SendAsync(cancellationToken)
                    .AsText();

                return response?.ToHttpUriOrNull();
            }
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return AsyncEnumerable.Empty<ItemInfo>();
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return default;
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return AsyncEnumerable.Empty<ItemInfo>();
        }
    }
}

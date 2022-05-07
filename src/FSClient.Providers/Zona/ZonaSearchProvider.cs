namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class ZonaSearchProvider : ISearchProvider
    {
        private readonly ZonaSiteProvider siteProvider;

        public ZonaSearchProvider(ZonaSiteProvider zonaSiteProvider)
        {
            siteProvider = zonaSiteProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.CreateDefault(SectionModifiers.Film),
            Section.CreateDefault(SectionModifiers.Serial)
        };

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return Task.FromResult((IEnumerable<ItemInfo>)new[] { original });
            }

            return original
                .GetTitles()
                .Select(t => new Func<CancellationToken, Task<IEnumerable<ItemInfo>>>(ct =>
                    GetFullResultInternal(t, original.Section, original.Details.Year, ct)
                        .Take(IncrementalLoadingCollection.DefaultCount)
                        .ToEnumerableAsync(ct)))
                .WhenAny(item => item?.Any() == true, Enumerable.Empty<ItemInfo>(), token: cancellationToken);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section));
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter.SearchRequest, filter.PageParams.Section, null);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
#pragma warning disable IDE0060 // Remove unused parameter
            string search, Section section, int? year,
#pragma warning restore IDE0060 // Remove unused parameter
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int page = 0, maxPage = 1;
            var searchRequest = Uri.EscapeDataString(search);
            var isSerial = section.Modifier.HasFlag(SectionModifiers.Serial);

            while (page < maxPage)
            {
                var currentPage = ++page;

                var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

                var json = await siteProvider
                    .HttpClient
                    .GetBuilder(new Uri(domain, $"/api/v1/search/{searchRequest}"))
                    .WithArgument("page", currentPage.ToString())
                    .WithAjax()
                    .SendAsync(cancellationToken)
                    .AsNewtonsoftJson<JObject>()
                    .ConfigureAwait(false);

                if (json == null
                    || json["query"]?["second"]?.Type != JTokenType.Null
                    || json["is_second"]?.ToBoolOrNull() == true
                    || json["items"] is not JArray jItems)
                {
                    yield break;
                }

                maxPage = json["pagination"]?["total_pages"]?.ToIntOrNull() ?? -1;

                var items = jItems
                    .OfType<JObject>()
                    .Select(item => ParseItemFromJObject(item, siteProvider.Site, domain))
                    .Where(item => !string.IsNullOrEmpty(item.SiteId)
                        && item.Link != null
                        && item.Section.Modifier.HasFlag(SectionModifiers.Serial) == isSerial);

                foreach (var item in items)
                {
                    yield return item;
                }
            }
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetShortResultInternal(request, section);
        }

        private async IAsyncEnumerable<ItemInfo> GetShortResultInternal(string request, Section section,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var json = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, $"/api/v1/suggest/{request}"))
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            if (json?["items"] is not JArray items)
            {
                yield break;
            }

            var results = items
                .OfType<JObject>()
                .Select(item => ParseItemFromJObject(item, Site, domain))
                .Where(item => !string.IsNullOrEmpty(item.SiteId)
                    && item.Section == section);
            foreach (var item in results)
            {
                yield return item;
            }
        }

        private static ItemInfo ParseItemFromJObject(JObject json, Site site, Uri domain)
        {
            var title = json["name_rus"]?.ToString()?.NotEmptyOrNull();
            var enTitle = json["name_eng"]?.ToString()?.NotEmptyOrNull()
                ?? json["name_original"]?.ToString()?.NotEmptyOrNull();

            if (title == null)
            {
                title = enTitle;
                enTitle = null;
            }

            var info = new ItemInfo(site, json["mobi_link_id"]?.ToString())
            {
                Title = title,
                Section = Section.CreateDefault(json["serial"]?.ToBoolOrNull() == true ? SectionModifiers.Serial : SectionModifiers.Film),
                Poster = new WebImage
                {
                    [ImageSize.Thumb] = json["cover"]?.ToUriOrNull()
                },
                Link = new Uri(domain, $"{(json["serial"]?.ToBoolOrNull() == true ? "tvseries" : "movies")}/{json["name_id"]}"),
                Details =
                {
                    Year = (json["year"] ?? json["serial_end_year"])?.ToIntOrNull(),
                    TitleOrigin = enTitle
                }
            };
            if (json["id"]?.ToString() is string kpId)
            {
                info.Details.LinkedIds.Add(Sites.Kinopoisk, kpId);
            }
            return info;
        }
    }
}

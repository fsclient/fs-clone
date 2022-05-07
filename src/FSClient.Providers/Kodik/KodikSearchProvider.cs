namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class KodikSearchProvider : ISearchProvider
    {
        private readonly KodikSiteProvider siteProvider;

        public KodikSearchProvider(
            KodikSiteProvider kodikSiteProvider)
        {
            siteProvider = kodikSiteProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.CreateDefault(SectionModifiers.Film),
            Section.CreateDefault(SectionModifiers.Serial)
        };

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return new[] { original };
            }

            if (original.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpIdStr)
                && int.TryParse(kpIdStr, out var kpId))
            {
                return await GetFullResultInternal(original.Title ?? string.Empty, original.Section, kpId, original.Details.Year, cancellationToken)
                    .Take(IncrementalLoadingCollection.DefaultCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return await original
                .GetTitles()
                .Select(title => new Func<CancellationToken, Task<IEnumerable<ItemInfo>>>(
                    async ct => await GetFullResultInternal(title, original.Section, null, original.Details.Year, cancellationToken)
                        .Take(IncrementalLoadingCollection.DefaultCount)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false)))
                .WhenAny(item => item?.Any() == true, Enumerable.Empty<ItemInfo>(), token: cancellationToken)
                .ConfigureAwait(false);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section, DisplayItemMode.Minimal));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResultInternal(request, section, null, null);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter.SearchRequest, filter.PageParams.Section, null, null);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            string searchRequest, Section section, int? kpId, int? year,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var args = new Dictionary<string, string?>
            {
                //["year"] = year?.ToString(),
                //["strict"] = "true",
                ["token"] = Secrets.KodikApiKey
            };
            if (kpId.HasValue)
            {
                args.Add("kinopoisk_id", kpId.ToString());
            }
            else
            {
                args.Add("title", searchRequest);
            }

            var apiLink = new Uri(siteProvider.Properties[KodikSiteProvider.KodikApiDomainKey]);
            var result = await siteProvider.HttpClient
                .GetBuilder(new Uri(apiLink, "/search"))
                .WithArguments(args)
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JToken>()
                .ConfigureAwait(false);

            var results = result as JArray ?? (result as JObject)?["results"] as JArray;

            if (results == null)
            {
                yield break;
            }

            var items = results
                .OfType<JObject>()
                .Select(item =>
                {
                    var title = item["title"]?.ToString();
                    var titleOrigin = item["title_orig"]?.ToString();
                    var kinopoiskId = item["kinopoisk_id"]?.ToIntOrNull();
                    var itemYear = item["year"]?.ToIntOrNull();
                    var playerLink = item["link"]?.ToUriOrNull(apiLink);

                    var prox = searchRequest == null ? 0 : Math.Max(
                        title?.Proximity(searchRequest, false) ?? 0,
                        titleOrigin?.Proximity(searchRequest, false) ?? 0);

                    if (kpId.HasValue)
                    {
                        if (kpId != kinopoiskId)
                        {
                            return default;
                        }
                    }
                    else if ((year.HasValue
                        && year != itemYear)
                        || prox < 0.9)
                    {
                        return default;
                    }

                    var kodikId = playerLink?
                        .Segments
                        .Skip(2)
                        .FirstOrDefault()?
                        .Trim('/')?
                        .ToIntOrNull();

                    var itemId = $"kdk{kodikId}" + (kinopoiskId.HasValue ? ("_kp" + kinopoiskId) : "");
                    var type = item["type"]?.ToString() ?? string.Empty;
                    var isSerial = (playerLink?.Segments.Contains("serial/") ?? false)
                        || type.Contains("serial");
                    var isAnime = type.Contains("anime");

                    var itemInfo = new ItemInfo(siteProvider.Site, itemId)
                    {
                        Title = title,
                        Link = playerLink,
                        Section = Section.CreateDefault(
                            (isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
                            | (isAnime ? SectionModifiers.Anime : SectionModifiers.None)),
                        Details =
                        {
                            TitleOrigin = titleOrigin,
                            Year = itemYear
                        }
                    };

                    if (kinopoiskId.HasValue)
                    {
                        itemInfo.Details.LinkedIds.Add(Sites.Kinopoisk, kinopoiskId.ToString());
                    }

                    return (prox, item: itemInfo);
                })
                .Where(t => t.item?.SiteId != null && t.item.Link != null
                    && t.item.Section.Modifier.HasFlag(section.Modifier))
                .OrderBy(t => t.prox)
                .GroupBy(t => t.item.SiteId, t => t.item)
                .Select(g => g.First());

            foreach (var item in items)
            {
                yield return item;
            }
        }
    }
}

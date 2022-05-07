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

    public class CollapsSearchProvider : ISearchProvider
    {
        private readonly CollapsSiteProvider siteProvider;

        public CollapsSearchProvider(CollapsSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.Any
        };

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site && original.Link != null && original is CollapsItemInfo)
            {
                return new[] { original };
            }

            if (original.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpIdStr)
                && int.TryParse(kpIdStr, out var kpId))
            {
                return await GetFullResult(null, original.Section, kpId, null, cancellationToken)
                    .Take(IncrementalLoadingCollection.DefaultCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return await original
                .GetTitles()
                .Select(title => new Func<CancellationToken, Task<List<ItemInfo>>>(
                    ct => GetFullResult(title, original.Section, null, original.Details.Year, ct)
                        .Take(IncrementalLoadingCollection.DefaultCount)
                        .ToListAsync(ct)
                        .AsTask()))
                .WhenAny(item => item?.Any() == true, new List<ItemInfo>(), token: cancellationToken)
                .ConfigureAwait(false);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResult(filter.SearchRequest, filter.PageParams.Section, null, filter.Year?.Start.Value);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResult(request, section, null, null);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResult(
            string? searchRequest, Section section, int? kpIdFilter, int? yearFilter,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var args = new Dictionary<string, string?>
            {
                ["token"] = Secrets.CollapsApiKey
            };
            if (kpIdFilter.HasValue)
            {
                args.Add("kinopoisk_id", kpIdFilter.ToString());
            }
            else if (searchRequest != null)
            {
                args.Add("name", searchRequest);
                if (yearFilter.HasValue)
                {
                    args.Add("year", yearFilter.Value.ToString());
                }
            }

            var result = await siteProvider.HttpClient
                .GetBuilder(new Uri(domain, "list"))
                .WithArguments(args)
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            var results = result?["results"] as JArray;

            if (results == null)
            {
                yield break;
            }

            var items = results
                .OfType<JObject>()
                .Select(item =>
                {
                    var itemInfo = CollapsItemInfoProvider.GetItemFromJObject(siteProvider.Site, domain, item);

                    var prox = searchRequest == null ? 0
                        : Math.Max(
                            itemInfo.Title?.Proximity(searchRequest, false) ?? 0,
                            itemInfo.Details.TitleOrigin?.Proximity(searchRequest, false) ?? 0);

                    if (kpIdFilter.HasValue
                        && itemInfo.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kinopoiskId))
                    {
                        if (kpIdFilter != int.Parse(kinopoiskId))
                        {
                            return default;
                        }
                    }
                    else if (prox < 0.9)
                    {
                        return default;
                    }

                    return (prox, item: itemInfo);
                })
                .Where(t => t.item?.SiteId != null && t.item.Link != null
                    && t.item.Section.Modifier.HasFlag(section.Modifier))
                .OrderBy(t => t.prox)
                .GroupBy(t => t.item.SiteId, t => t.item)
                .Select(group => group
                    .OrderBy(item => string.IsNullOrEmpty(item.Title))
                    .ThenBy(item => !item.Details.LinkedIds.ContainsKey(Sites.Kinopoisk))
                    .First())
                .Cast<ItemInfo>();

            foreach (var item in items)
            {
                yield return item;
            }
        }
    }
}

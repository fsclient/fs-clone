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

    public class BazonSearchProvider : ISearchProvider
    {
        private readonly BazonSiteProvider siteProvider;
        private readonly YohohoSearchProvider? yohohoSearchProvider;

        public BazonSearchProvider(
            BazonSiteProvider siteProvider,
            YohohoSearchProvider? yohohoSearchProvider)
        {
            this.siteProvider = siteProvider;
            this.yohohoSearchProvider = yohohoSearchProvider;
        }

        public IReadOnlyList<Section> Sections => Array.Empty<Section>();

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site && original.Link != null && original is BazonItemInfo)
            {
                return new[] { original };
            }

            if (yohohoSearchProvider != null
                && bool.TryParse(siteProvider.Properties[BazonSiteProvider.BazonForceYohohoSearchKey], out var forceYohohoSearch)
                && forceYohohoSearch)
            {
                var item = await GetFromYohohoAsync(original, cancellationToken).ConfigureAwait(false);
                if (item != null)
                {
                    return new[] { item };
                }
                return Enumerable.Empty<ItemInfo>();
            }

            IEnumerable<ItemInfo> results;
            if (original.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpIdStr)
                && int.TryParse(kpIdStr, out var kpId))
            {
                results = await GetFullResult(null, original.Section, kpId, null, false, true, cancellationToken)
                    .Take(IncrementalLoadingCollection.DefaultCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                results = await original
                    .GetTitles()
                    .Select(title => new Func<CancellationToken, Task<List<ItemInfo>>>(
                        ct => GetFullResult(title, original.Section, null, original.Details.Year, false, true, ct)
                            .Take(IncrementalLoadingCollection.DefaultCount)
                            .ToListAsync(ct)
                            .AsTask()))
                    .WhenAny(item => item?.Any() == true, new List<ItemInfo>(), token: cancellationToken)
                    .ConfigureAwait(false);
            }

            if (yohohoSearchProvider != null
                && !results.Any())
            {
                var item = await GetFromYohohoAsync(original, cancellationToken).ConfigureAwait(false);
                if (item != null)
                {
                    return new[] { item };
                }
            }

            return results;
        }

        private async Task<ItemInfo?> GetFromYohohoAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (yohohoSearchProvider == null)
            {
                return null;
            }

            var result = await yohohoSearchProvider.GetRelatedResultsAsync(original, false, cancellationToken).ConfigureAwait(false);
            if (result.TryGetValue("bazon", out var bazonFromYohoho)
                && bazonFromYohoho.IFrame?.ToUriOrNull() is Uri iframe)
            {
                original.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpIdStr);

                var bazonId = iframe.Segments.LastOrDefault()?.Trim('/').GetDeterministicHashCode();
                if (kpIdStr == null && bazonId == null)
                {
                    return null;
                }

                var item = new ItemInfo(Site, kpIdStr ?? $"bzn{bazonId}")
                {
                    Title = bazonFromYohoho.Translate ?? original.Title,
                    Link = iframe
                };

                if (kpIdStr != null)
                {
                    item.Details.LinkedIds.Add(Sites.Kinopoisk, kpIdStr);
                }

                return item;
            }

            return null;
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResult(filter.SearchRequest, filter.PageParams.Section, null, filter.Year?.Start.Value, true, false);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section, minimumRequestLength: 2));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResult(request, section, null, null, true, false);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResult(
            string? searchRequest, Section section, int? kpIdFilter, int? year, bool distinctById, bool takeSingleId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var apiDomain = siteProvider.Properties[BazonSiteProvider.BazonApiLinkKey]?.ToUriOrNull();
            if (apiDomain == null)
            {
                yield break;
            }

            var args = new Dictionary<string, string?>
            {
                ["token"] = Secrets.BazonApiKey
            };
            if (kpIdFilter.HasValue)
            {
                args.Add("kp", kpIdFilter.ToString());
            }
            else if (searchRequest != null)
            {
                args.Add("title", searchRequest);
            }

            var result = await siteProvider.HttpClient
                .GetBuilder(new Uri(apiDomain, "api/search"))
                .WithArguments(args)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            var results = result?["results"] as JArray;

            if (results == null)
            {
                yield break;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var items = results
                .OfType<JObject>()
                .Select(item =>
                {
                    var itemInfo = BazonItemInfoProvider.GetItemFromJObject(siteProvider.Site, apiDomain, item);
                    if (itemInfo == null)
                    {
                        return default;
                    }

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
                    else if (prox < 0.9
                        || (year.HasValue && itemInfo.Details.Year != year))
                    {
                        return default;
                    }

                    return (prox, item: itemInfo);
                })
                .Where(t => t.item?.SiteId != null && t.item.Link != null
                    && t.item.Section.Modifier.HasFlag(section.Modifier))
                .OrderBy(t => t.prox)
                .ThenBy(t => string.IsNullOrEmpty(t.item.Title))
                .ThenBy(t => !t.item.Details.LinkedIds.ContainsKey(Sites.Kinopoisk))
                .Select(t => t.item)
                .Cast<ItemInfo>();

            if (distinctById)
            {
                items = items.DistinctBy(i => i.SiteId);
            }
            if (takeSingleId
                && items.FirstOrDefault()?.SiteId is string siteId)
            {
                items = items.Where(i => i.SiteId == siteId);
            }

            foreach (var item in items)
            {
                yield return item;
            }
        }
    }
}

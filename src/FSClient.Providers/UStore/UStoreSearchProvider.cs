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

    public class UStoreSearchProvider : ISearchProvider
    {
        private readonly UStoreSiteProvider siteProvider;
        private readonly YohohoSearchProvider? yohohoSearchProvider;
        private const double similarMinProx = 0.9;
        private const double searchMinProx = 0.8;

        public UStoreSearchProvider(
            UStoreSiteProvider siteProvider,
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
            if (original.Site == Site && original.Link != null && original is UStoreItemInfo info)
            {
                return new[] { info };
            }

            if (bool.TryParse(siteProvider.Properties[UStoreSiteProvider.UStoreForceYohohoSearchKey], out var forceYohohoSearch)
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
                results = await GetFullResultInternal(null, original.Section, kpId, null, null, false, true, similarMinProx, cancellationToken)
                    .Take(IncrementalLoadingCollection.DefaultCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (original.Details.LinkedIds.TryGetValue(Sites.IMDb, out var imdbIdStr))
            {
                results = await GetFullResultInternal(null, original.Section, null, imdbIdStr, null, false, true, similarMinProx, cancellationToken)
                    .Take(IncrementalLoadingCollection.DefaultCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                results = await original
                    .GetTitles()
                    .Select(title => new Func<CancellationToken, Task<List<ItemInfo>>>(
                        ct => GetFullResultInternal(title, original.Section, null, null, original.Details.Year, false, true, similarMinProx, ct)
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
            if (result.TryGetValue("ustore", out var ustoreFromYohoho)
                && ustoreFromYohoho.IFrame?.ToUriOrNull() is Uri iframe)
            {
                original.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpIdStr);
                
                var ustoreId = iframe.Segments.LastOrDefault()?.Trim('/').GetDeterministicHashCode();
                if (kpIdStr == null && ustoreId == null)
                {
                    return null;
                }

                var item = new ItemInfo(Site, kpIdStr ?? $"ust{ustoreId}")
                {
                    Title = ustoreFromYohoho.Translate ?? original.Title,
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
            return GetFullResultInternal(filter.SearchRequest, filter.PageParams.Section, null, null, null, true, false, searchMinProx);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return default;
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResultInternal(request, section, null, null, null, true, false, searchMinProx);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            string? searchRequest, Section section, int? kpIdFilter, string? imdbIdFilter, int? year, bool distinctById, bool takeSingleId, double minProx,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var apiDomain = siteProvider.Properties[UStoreSiteProvider.UStoreApiLinkKey]?.ToUriOrNull();
            if (apiDomain == null)
            {
                yield break;
            }

            var args = new Dictionary<string, string?>
            {
                ["hash"] = Secrets.UStoreApiKey
            };
            if (kpIdFilter.HasValue)
            {
                args.Add("f", "search_by_id");
                args.Add("id", kpIdFilter.ToString());
                args.Add("where", "kinopoisk");
            }
            else if (imdbIdFilter != null)
            {
                args.Add("f", "search_by_id");
                args.Add("id", imdbIdFilter);
                args.Add("where", "imdb");
            }
            else if (searchRequest != null)
            {
                args.Add("f", "search_by_title");
                args.Add("title", searchRequest);
            }

            var response = await siteProvider.HttpClient
                .GetBuilder(apiDomain)
                .WithArguments(args)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JToken>()
                .ConfigureAwait(false);

            if (response is not JArray results)
            {
                yield break;
            }

            var items = results
                .OfType<JObject>()
                .Select(item =>
                {
                    var itemInfo = UStoreItemInfoProvider.GetItemFromJObject(siteProvider.Site, apiDomain, item);
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
                    else if (imdbIdFilter != null
                        && itemInfo.Details.LinkedIds.TryGetValue(Sites.IMDb, out var imdbId))
                    {
                        if (imdbIdFilter != imdbId)
                        {
                            return default;
                        }
                    }
                    else if (prox < minProx
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

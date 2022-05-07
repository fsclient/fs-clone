namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class KinoPubSearchProvider : ISearchProvider
    {
        private readonly KinoPubSiteProvider siteProvider;

        public KinoPubSearchProvider(
            KinoPubSiteProvider kinoPubSiteProvider)
        {
            siteProvider = kinoPubSiteProvider;

            Sections = KinoPubSiteProvider.KinoPubSections;
        }

        public IReadOnlyList<Section> Sections { get; }

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.ProForAny;

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section, DisplayItemMode.Detailed));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResultInternal(request, section);
        }

        public Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return Task.FromResult((IEnumerable<ItemInfo>)new[] { original });
            }

            return original.GetTitles()
                .Select(title => new Func<CancellationToken, Task<IEnumerable<ItemInfo>>>(async ct =>
                {
                    if (title == null)
                    {
                        return Enumerable.Empty<ItemInfo>();
                    }

                    return await GetShortResult(title, Section.Any)
                        .Take(IncrementalLoadingCollection.DefaultCount)
                        .Where(item => !original.Details.Year.HasValue || item.Details.Year == original.Details.Year)
                        .Select(item => new
                        {
                            Prox = Math.Max(
                                item.Title?.Replace("4К", "").Proximity(title, false) ?? 0,
                                item.Details.TitleOrigin?.Proximity(title, false) ?? 0),
                            Value = item
                        })
                        .Where(obj => obj.Prox > 0.9)
                        .OrderByDescending(obj => obj.Prox)
                        .Select(obj => obj.Value)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);
                }))
                .WhenAny(res => res?.Any() == true, Enumerable.Empty<ItemInfo>(), cancellationToken);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter.SearchRequest, filter.PageParams.Section);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            string request, Section section,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int page = 0, maxPage = 1;

            while (page < maxPage)
            {
                var currentPage = ++page;

                var result = await siteProvider
                    .GetAsync(
                        "items/search",
                        new Dictionary<string, string>
                        {
                            ["q"] = request,
                            ["page"] = currentPage.ToString(),
                            ["type"] = section == Section.Any ? "" : section.Value,
                            ["field"] = "title",
                            ["perpage"] = KinoPubSiteProvider.ItemsPerPage.ToString(),
                            ["sectioned"] = "0"
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                if (result == null
                    || result["items"] is not JArray jItems
                    || cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                maxPage = result["pagination"]?["total"]?.ToIntOrNull()
                    ?? currentPage - 1;

                if (maxPage < currentPage)
                {
                    yield break;
                }

                var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

                var items = jItems
                   .OfType<JObject>()
                   .Select(obj => siteProvider.ParseFromJson(obj, domain))
                   .Where(item => item?.SiteId != null && item.Section != default);

                foreach (var item in items)
                {
                    yield return item;
                }
            }
        }
    }
}

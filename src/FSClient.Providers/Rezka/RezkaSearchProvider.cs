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

    public class RezkaSearchProvider : ISearchProvider
    {
        private readonly RezkaSiteProvider siteProvider;

        public RezkaSearchProvider(RezkaSiteProvider rezkaSiteProvider)
        {
            siteProvider = rezkaSiteProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.Any
        };

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return Task.FromResult((IEnumerable<ItemInfo>)new[] { original });
            }

            var originalTitles = original.GetTitles().ToArray();
            return originalTitles
                .Select(t => new Func<CancellationToken, Task<IEnumerable<ItemInfo>>>(
                    async ct =>
                    {
                        IEnumerable<ItemInfo> items = await GetFullResultInternal(t, ct)
                            .Take(IncrementalLoadingCollection.DefaultCount)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

                        if (original.Details.Year.HasValue)
                        {
                            items = items.Where(i => i.Details.Year == original.Details.Year);
                        }
                        if (original.Section != default)
                        {
                            var requestedFlag = original.Section.Modifier.HasFlag(SectionModifiers.Serial)
                                ? SectionModifiers.Serial
                                : SectionModifiers.Film;
                            items = items.Where(i => i.Section.Modifier.HasFlag(requestedFlag));
                        }

                        items = items.Select(i => (i, p: i.GetTitles().MaxOrDefault(t => originalTitles.MaxOrDefault(c => c.Proximity(t)))))
                            .Where(t => t.p > 0.85)
                            .OrderByDescending(t => t.p)
                            .Select(t => t.i)
                            .ToArray();

                        return items;
                    }))
                .WhenAny(item => item?.Any() == true, Enumerable.Empty<ItemInfo>(), token: cancellationToken);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResultInternal(request);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter.SearchRequest);
        }

        public async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            string searchRequest, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int page = 0, maxPage = 1;

            while (page < maxPage)
            {
                var currentPage = ++page;

                var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

                var html = (await siteProvider
                    .HttpClient
                    .GetBuilder(new Uri(domain, "search/"))
                    .WithArgument("do", "search")
                    .WithArgument("subaction", "search")
                    .WithArgument("q", searchRequest)
                    .WithArgument("page", currentPage.ToString())
                    .WithHeader("Referer", domain.ToString())
                    .SendAsync(cancellationToken)
                    .AsHtml(cancellationToken)
                    .ConfigureAwait(false))?
                    .QuerySelector(".b-content__search_wrapper");

                if (html == null)
                {
                    yield break;
                }

                maxPage = html.QuerySelectorAll(".b-navigation a[href*=page]")
                    .Select(a => a.TextContent?.ToIntOrNull())
                    .Where(page => page.HasValue)
                    .OrderByDescending(page => page)
                    .FirstOrDefault() ?? -1;

                var items = html.QuerySelectorAll(".b-content__inline_item[data-id][data-url]")
                    .Select(htmlItem => RezkaItemInfoProvider.ParseItemInfoFromTileHtml(Site, domain, htmlItem))
                    .Where(item => !string.IsNullOrEmpty(item.SiteId));

                foreach (var item in items)
                {
                    yield return item!;
                }
            }
        }
    }
}

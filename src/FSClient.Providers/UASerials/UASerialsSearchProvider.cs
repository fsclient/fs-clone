namespace FSClient.Providers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class UASerialsSearchProvider : ISearchProvider
    {
        private readonly UASerialsSiteProvider siteProvider;
        private readonly UASerialsItemInfoProvider itemInfoProvider;

        public UASerialsSearchProvider(
            UASerialsSiteProvider siteProvider,
            UASerialsItemInfoProvider itemInfoProvider)
        {
            this.siteProvider = siteProvider;
            this.itemInfoProvider = itemInfoProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.CreateDefault(SectionModifiers.Serial),
            Section.CreateDefault(SectionModifiers.Cartoon | SectionModifiers.Serial),
            Section.CreateDefault(SectionModifiers.TVShow | SectionModifiers.Serial)
        };

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site && original is UASerialsItemInfo uaSerialsItemInfo && uaSerialsItemInfo.DataTag != null)
            {
                var item = await siteProvider.EnsureItemAsync(original, cancellationToken).ConfigureAwait(false);
                return new[] { item };
            }

            if (original.Section.Modifier.HasFlag(SectionModifiers.Film))
            {
                return Enumerable.Empty<ItemInfo>();
            }

            var itemYear = original.Details.Year;

            var localCache = new ConcurrentDictionary<string, ItemInfo>();

            return await original.GetTitles(true)
                .ToAsyncEnumerable()
                .SelectMany(title => GetShortResult(title, original.Section)
                    .Select(item => (
                        prox: Math.Max(
                            item.Title?.Proximity(title, false) ?? 0,
                            item.Details.TitleOrigin?.Proximity(original.Details.TitleOrigin ?? "", false) ?? 0),
                        value: item
                    ))
                    .Where(tuple => tuple.value.SiteId != null && tuple.prox > 0.9)
                    .OrderByDescending(tuple => tuple.prox)
                    .Select(tuple => tuple.value))
                .SelectAwaitWithCancellation(async (item, ct) =>
                {
                    if (localCache.TryGetValue(item.SiteId!, out var itemInfo))
                    {
                        return itemInfo;
                    }

                    await itemInfoProvider.PreloadItemAsync(item, PreloadItemStrategy.Poster, ct).ConfigureAwait(false);
                    localCache.TryAdd(item.SiteId!, item);

                    return item;
                })
                .Where(item => ((UASerialsItemInfo)item).DataTag != null)
                .SkipWhile(item => !itemYear.HasValue || item.Details.Year != itemYear)
                .Take(1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section, DisplayItemMode.Minimal));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResultInternal(request, section);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter.SearchRequest, filter.PageParams.Section);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            string search, Section sectionFilter,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var (category, section) = sectionFilter.Modifier.HasFlag(SectionModifiers.Cartoon) ? ("cartoon/", Sections[1])
                : sectionFilter.Modifier.HasFlag(SectionModifiers.TVShow) ? ("series/documentary/", Sections[2])
                : sectionFilter.Modifier.HasFlag(SectionModifiers.Serial) ? ("series/", Sections[0]) : default;

            if (category == null)
            {
                yield break;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var html = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, category))
                .WithBody(new Dictionary<string, string>
                {
                    ["do"] = "search",
                    ["subaction"] = "subaction",
                    ["story"] = search
                })
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", domain.ToString())
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (html == null)
            {
                yield break;
            }

            var items = html
                .QuerySelectorAll(".short-item")
                .Select(div =>
                {
                    var link = div.QuerySelector("a.short-img[href]")?.GetAttribute("href")?.ToUriOrNull(domain);
                    var id = link?.GetPath().SplitLazy(2, StringSplitOptions.RemoveEmptyEntries, '/', '-').FirstOrDefault();
                    if (id == null || link == null)
                    {
                        return null;
                    }

                    var poster = div.QuerySelector(".short-img img[src]")?.GetAttribute("src")?.ToUriOrNull(domain);
                    var uaTitle = div.QuerySelector(".th-title")?.TextContent;
                    var enTitle = div.QuerySelector(".th-title-oname")?.TextContent?.SplitLazy(2, '/').FirstOrDefault()?.Trim();

                    return new UASerialsItemInfo(siteProvider.Site, id.ToString())
                    {
                        Link = link,
                        Poster = poster,
                        Title = uaTitle,
                        Section = section,
                        Details =
                        {
                            TitleOrigin = enTitle
                        }
                    };
                })
                .Where(i => i != null);

            foreach (var item in items)
            {
                yield return item!;
            }
        }
    }
}

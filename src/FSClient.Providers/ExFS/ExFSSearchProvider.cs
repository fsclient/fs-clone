namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Html.Dom;

    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class ExFSSearchProvider : ISearchProvider
    {
        private readonly ExFSSiteProvider siteProvider;

        public ExFSSearchProvider(
            ExFSSiteProvider exFsSiteProvider)
        {
            siteProvider = exFsSiteProvider;
        }

        public Site Site => siteProvider.Site;

        public IReadOnlyList<Section> Sections => ExFSSiteProvider.Sections;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetShortResultInternal(request, section);
        }

        private async IAsyncEnumerable<ItemInfo> GetShortResultInternal(string request, Section section,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var html = await siteProvider
                .HttpClient
                .PostBuilder(domain)
                .WithBody(new Dictionary<string, string>
                {
                    ["do"] = "search",
                    ["subaction"] = "search",
                    ["story"] = request
                })
                .WithAjax()
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            if (html != null)
            {
                foreach (var item in ParseFromPage(html, domain, section))
                {
                    yield return item;
                }
            }
        }

        private static IEnumerable<ItemInfo> ParseFromPage(IHtmlDocument page, Uri domain, Section section)
        {
            return page
                .QuerySelectorAll(".SeaRchresultPost")
                .Select(el =>
                {
                    var anchor = el.QuerySelector(".SeaRchresultPostTitle a");
                    if (anchor == null
                        || !Uri.TryCreate(domain, anchor.GetAttribute("href"), out var l))
                    {
                        return null;
                    }

                    var id = ExFSSiteProvider.GetIdFromUrl(l);

                    var genres = el
                        .QuerySelectorAll(".SeaRchresultPostInfo a")
                        .Select(e => ExFSSiteProvider.GetTagFromLinkString(e.TextContent, e.GetAttribute("href")))
                        .Where(ln => ln != TitledTag.Any
                            && ln.Type == "genre")
                        .ToArray();

                    var yearStr = el.QuerySelectorAll(".SeaRchresultPostInfo a")?
                        .FirstOrDefault(y => y.GetAttribute("href")?
                        .Contains("year") ?? false)?
                        .TextContent;

                    return new ItemInfo(Sites.ExFS, id)
                    {
                        Poster = ExFSSiteProvider.GetPoster(domain, el.QuerySelector(".SeaRchresultPostPoster img")?.GetAttribute("src")) ?? default,
                        Link = l,
                        Section = ExFSSiteProvider.GetSectionFromUrl(l),
                        Details =
                        {
                            Titles = anchor.TextContent?.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray() ?? Array.Empty<string>(),
                            Description = el.QuerySelector(".SeaRchresultPostOpisanie")?.TextContent?.Trim(),
                            Year = int.TryParse(yearStr, out var year) ? year : (int?)null,
                            Tags = new[]
                            {
                                new TagsContainer(TagType.Genre, genres)
                            }
                        }
                    };
                })
                .Where(i => i != null
                        && i.Section != Section.Any
                        && (section == Section.Any || i.Section == section))!;
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section, DisplayItemMode.Detailed, 4));
        }

        public Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return Task.FromResult((IEnumerable<ItemInfo>)new[] { original });
            }

            return Task.FromResult(new[] {
                new ItemInfo(Site, $"exfs{original.SiteId}")
                {
                    Title = original.Title,
                    Section = original.Section,
                    Details =
                    {
                        TitleOrigin = original.Details.TitleOrigin,
                        Year = original.Details.Year
                    }
                }
            }.AsEnumerable());
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            SearchPageFilter filter,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int page = 0, maxPage = 1;
            var request = filter.SearchRequest;
            var section = filter.PageParams.Section;

            var searchedPages = new List<int>();

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            while (page < maxPage)
            {
                var currentPage = ++page;
                while (searchedPages.Contains(page))
                {
                    currentPage = ++page;
                }

                searchedPages.Add(currentPage);
                var html = await siteProvider
                    .HttpClient
                    .PostBuilder(domain)
                    .WithBody(new Dictionary<string, string>
                    {
                        ["do"] = "search",
                        ["subaction"] = "search",
                        ["story"] = request,
                        ["search_start"] = currentPage.ToString()
                    })
                    .WithAjax()
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .SendAsync(cancellationToken)
                    .AsHtml(cancellationToken)
                    .ConfigureAwait(false);

                if (html == null)
                {
                    yield break;
                }

                var navPages = html.QuerySelectorAll(".navigations a, .navigations span")
                    .Select(s => int.TryParse(s.TextContent?.Trim(), out var t) ? t : -1)
                    .Where(p => p >= 0);
                maxPage = navPages.Any()
                    ? navPages.Max()
                    : currentPage - 1;

                foreach (var item in ParseFromPage(html, domain, section))
                {
                    yield return item;
                }
            }
        }
    }
}

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
    using FSClient.Shared.Providers;

    public class AnitubeSearchProvider : ISearchProvider
    {
        private readonly AnitubeSiteProvider siteProvider;

        public AnitubeSearchProvider(AnitubeSiteProvider siteProvider)
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
            if (original.Site == Site)
            {
                return new[] { original };
            }

            if (!original.Section.Modifier.HasFlag(SectionModifiers.Cartoon))
            {
                return Enumerable.Empty<ItemInfo>();
            }

            var year = original.Details.Year;

            // It's better to search by english name on ukrainian site.
            // But it doesn't provie TitleOrigin, so we shouldn't try to filter by proximity.
            var titles = original.GetTitles();
            return await original
                .GetTitles(true)
                .Select(t => new Func<CancellationToken, Task<List<ItemInfo>>>(ct => GetShortResultInternal(t, ct)
                    .Select(item => new
                    {
                        Prox = Math.Max(
                            titles.Select(t => item.Title?.Proximity(t, false) ?? 0).Max(),
                            titles.Select(t => item.Details.TitleOrigin?.Proximity(t, false) ?? 0).Max()),
                        Value = item
                    })
                    .Where(obj => !year.HasValue || obj.Value.Details.Year == year)
                    .OrderByDescending(obj => obj.Prox)
                    .Select(obj => obj.Value)
                    .ToListAsync(ct)
                    .AsTask()))
                .WhenAny(item => item?.Any() == true, new List<ItemInfo>(), token: cancellationToken)
                .ConfigureAwait(false);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetShortResultInternal(filter.SearchRequest);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetShortResultInternal(request);
        }

        private async IAsyncEnumerable<ItemInfo> GetShortResultInternal(
            string request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var html = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "/engine/mod_punpun/dle_search/ajax/dle_search.php"))
                .WithBody(new Dictionary<string, string>
                {
                    ["query"] = request,
                    ["thisUrl"] = "/"
                })
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", domain.ToString())
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .WithAjax()
                .WithArgument("query", request)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (html == null)
            {
                yield break;
            }

            var items = html
                .QuerySelectorAll("a[href]")
                .Select(anchor =>
                {
                    var link = anchor.GetAttribute("href")?.ToUriOrNull(domain);
                    var id = link?.GetPath().SplitLazy(2, StringSplitOptions.RemoveEmptyEntries, new[] { '/', '-' }).FirstOrDefault()?.ToIntOrNull();

                    if (link == null || !id.HasValue)
                    {
                        return null;
                    }
                    var title = anchor.QuerySelector(".searchheading")?.TextContent?.SplitLazy(2, '(').FirstOrDefault()?.Trim();
                    var thumbnail = anchor.QuerySelector("img[src]")?.GetAttribute("src")?.ToUriOrNull();
                    var isFilm = html.QuerySelector("b:contains('Серій:')")?.NextSibling?.TextContent?.Contains("1 з 1") == true;
                    var year = anchor.QuerySelector("b:contains('Рік:')")?.NextSibling?.TextContent?.ToIntOrNull();

                    return new ItemInfo(Site, id.ToString())
                    {
                        Link = link,
                        Title = title,
                        Section = Section.CreateDefault(SectionModifiers.Anime
                            | (isFilm ? SectionModifiers.Film : SectionModifiers.Serial)),
                        Poster = thumbnail,
                        Details =
                        {
                            Year = year
                        }
                    };
                })
                .Where(item => !string.IsNullOrEmpty(item?.SiteId))!;

            foreach (var item in items)
            {
                yield return item!;
            }
        }
    }
}

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

    using Nito.AsyncEx;

    public class KinoUkrSearchProvider : ISearchProvider
    {
        private const int CornerYear = 2007;

        private readonly AsyncLazy<string?> logicHashLazy;
        private readonly KinoUkrSiteProvider siteProvider;

        public KinoUkrSearchProvider(KinoUkrSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            logicHashLazy = new AsyncLazy<string?>(GetLoginHashInternalAsync);
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.Any,
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

            var title = original.GetTitles(true).FirstOrDefault();
            if (string.IsNullOrEmpty(title))
            {
                return Enumerable.Empty<ItemInfo>();
            }

            var searchRequests = new[]
            {
                $"{title} {original.Details.Year}".Trim(),
                $"\"{title}\"",
                title
            };

            return await searchRequests
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((request, ct) => new ValueTask<IEnumerable<ItemInfo>>(
                    GetFullResultInternal(request, original.Details.Year, original.Section, ct)
                        .Take(IncrementalLoadingCollection.DefaultCount)
                        .ToEnumerableAsync(ct)))
                .FirstOrDefaultAsync(items => items?.Count() is int count && count > 0 && count < 15, cancellationToken)
                .ConfigureAwait(false)
                ?? Enumerable.Empty<ItemInfo>();
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section, minimumRequestLength: 4));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetShortResultInternal(request);
        }

        public async IAsyncEnumerable<ItemInfo> GetShortResultInternal(string request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var userHash = await GetLoginHashAsync(cancellationToken).ConfigureAwait(false);
            var html = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "/engine/ajax/controller.php?mod=search"))
                .WithBody(new Dictionary<string, string>
                {
                    ["query"] = request,
                    ["user_hash"] = userHash ?? string.Empty
                })
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", domain.ToString())
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (html is null)
            {
                yield break;
            }

            var items = html
                .QuerySelectorAll("a[href]")
                .Select(item =>
                {
                    var link = item.GetAttribute("href")?.ToUriOrNull(domain);
                    var id = link?.Segments.LastOrDefault()?.Split('-').FirstOrDefault()?.TrimStart('/');
                    var title = item.QuerySelector(".searchheading")?.TextContent;
                    var description = item.QuerySelector(".searchheading")?.NextElementSibling?.TextContent;
                    if (link == null || string.IsNullOrEmpty(id))
                    {
                        return null;
                    }
                    return new ItemInfo(siteProvider.Site, id)
                    {
                        Link = link,
                        Title = title,
                        Details =
                        {
                            Description = description
                        }
                    };
                })
                .Where(item => !string.IsNullOrEmpty(item?.SiteId))!;
            foreach (var item in items)
            {
                yield return item!;
            }
        }

        public Task<string?> GetLoginHashAsync(CancellationToken cancellationToken)
        {
            return logicHashLazy.Task.WaitAsync(cancellationToken);
        }

        private async Task<string?> GetLoginHashInternalAsync()
        {
            var mirror = await siteProvider.GetMirrorAsync(default).ConfigureAwait(false);
            var pageText = await siteProvider.HttpClient.GetBuilder(mirror).SendAsync(default).AsText().ConfigureAwait(false);
            if (pageText == null)
            {
                return null;
            }

            const string paramName = "dle_login_hash = '";
            var startIndex = pageText.IndexOf(paramName, StringComparison.Ordinal) + paramName.Length;
            if (startIndex < paramName.Length)
            {
                return null;
            }

            var endIndex = pageText.IndexOf("'", startIndex, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                return null;
            }

            return pageText[startIndex..endIndex];
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter.SearchRequest, filter.Year?.Start.Value, filter.PageParams.Section);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            string searchRequest, int? year, Section section,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var sectionModifier = section.Modifier;

            var beforeAfter = "after";
            var searchDate = 0;

            if (year is int fromYear)
            {
                if (fromYear < CornerYear)
                {
                    fromYear++;
                    beforeAfter = "before";
                }
                searchDate = (int)(DateTime.Now - new DateTime(fromYear, 1, 1)).TotalDays;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var html = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "/index.php?do=search"))
                .WithBody(new Dictionary<string, string>
                {
                    ["do"] = "search",
                    ["subaction"] = "search",
                    ["search_start"] = "0",
                    ["full_search"] = "1",
                    ["result_from"] = "1",
                    ["story"] = searchRequest,
                    ["titleonly"] = "0",
                    ["searchuser"] = "",
                    ["replyless"] = "0",
                    ["replylimit"] = "0",
                    ["searchdate"] = searchDate.ToString(),
                    ["beforeafter"] = beforeAfter,
                    ["sortby"] = "",
                    ["resorder"] = "desc",
                    ["showposts"] = "1",
                    ["catlist[]"] = sectionModifier == SectionModifiers.Serial ? "23"
                        : sectionModifier == SectionModifiers.Film ? "26"
                        : "0",
                })
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", domain.ToString())
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (html is null)
            {
                yield break;
            }

            var items = html
                .QuerySelectorAll("div.short-img")
                .Select(item =>
                {
                    var link = item.QuerySelector("[data-href]")?.GetAttribute("data-href")?.ToUriOrNull(domain);
                    var id = link?.Segments.LastOrDefault()?.Split('-').FirstOrDefault()?.TrimStart('/');
                    var poster = item.QuerySelector("img[src]")?.GetAttribute("src")?.ToUriOrNull(domain);
                    var title = item.QuerySelector("img[src]")?.GetAttribute("alt");
                    var description = item.NextElementSibling?.QuerySelector(".sd-line span:contains('Опис:')")?.NextSibling?.TextContent?.Trim();
                    var sectionInfo = item.QuerySelector(".m-meta.m-qual, .m-meta.m-series")?.TextContent;
                    var isSerial = sectionInfo?.Contains("Серіал") == true;
                    var isBlocked = sectionInfo?.Contains("Тимчасово не працює") == true;
                    var isCartoon = item.NextElementSibling?.QuerySelector(".sd-line:contains('Мультфільми'), .sd-line:contains('Аніме')") != null;

                    if (link == null || string.IsNullOrEmpty(id) || isBlocked)
                    {
                        return null;
                    }
                    return new ItemInfo(siteProvider.Site, id)
                    {
                        Link = link,
                        Poster = poster,
                        Title = title,
                        Section = Section.CreateDefault(
                            (isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
                            | (isCartoon ? SectionModifiers.Cartoon : SectionModifiers.None)),
                        Details =
                        {
                            Description = description
                        }
                    };
                })
                .Where(item => !string.IsNullOrEmpty(item?.SiteId)
                    && item!.Section.Modifier.HasFlag(sectionModifier));

            foreach (var item in items)
            {
                yield return item!;
            }
        }
    }
}

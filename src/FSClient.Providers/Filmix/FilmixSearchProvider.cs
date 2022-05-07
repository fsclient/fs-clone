namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    using Nito.AsyncEx;

    public class FilmixSearchProvider : ISearchProvider
    {
        private static readonly Dictionary<SortType, string> sortTypes = new Dictionary<SortType, string>
        {
            [SortType.UpdateDate] = "date",
            [SortType.Year] = "year",
            [SortType.Alphabet] = "title",
            [SortType.Commented] = "comm_num",
            [SortType.Visit] = "news_read",
            [SortType.Popularity] = "rating"
        };
        private static readonly TagsContainer[] tagsContainers = new[]
        {
            new TagsContainer(TagType.Genre, FilmixSiteProvider.Genres),
            new TagsContainer(TagType.County, FilmixSiteProvider.Countries),
            new TagsContainer(Strings.TagType_Quality, FilmixSiteProvider.Qualities)
        };

        private readonly FilmixSiteProvider siteProvider;
        private readonly AsyncLazy<(string, string)[]> missedHiddenArgs;

        public FilmixSearchProvider(
            FilmixSiteProvider filmixSiteProvider)
        {
            siteProvider = filmixSiteProvider;
            missedHiddenArgs = new AsyncLazy<(string, string)[]>(FetchHiddenInputs);
        }

        public IReadOnlyList<Section> Sections => FilmixSiteProvider.Sections;

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(
                Site, section, DisplayItemMode.Detailed, 2, true, true, new Range(1902, DateTime.Now.Year + 1), tagsContainers, sortTypes.Keys));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return EnumerableHelper
                .ToAsyncEnumerable(ct => siteProvider.GetMirrorAsync(ct).AsTask())
                .SelectAwaitWithCancellation((domain, ct) => new ValueTask<JToken?>(siteProvider
                    .HttpClient
                    .GetBuilder(new Uri(domain, "api/v2/suggestions"))
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .WithArgument("search_word", request)
                    .WithHeader("Origin", domain.GetOrigin())
                    .WithHeader("Referer", domain.ToString())
                    .WithAjax()
                    .SendAsync(ct)
                    .AsNewtonsoftJson<JToken>()))
                .TakeWhile(jToken => jToken != null)
                .SelectMany(jToken => (jToken as JArray ?? jToken?["posts"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .Select(jObject =>
                    {
                        var categories = jObject["categories"]?.ToString();
                        if (categories == null)
                        {
                            return (jObject, null);
                        }

                        var fragment = WebHelper.ParseHtml(categories);
                        return (jObject, fragment);
                    })
                    .Select(tuple =>
                    {
                        var id = tuple.jObject["id"]?.ToString();
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            return null;
                        }

                        var sectionSpan = (tuple.fragment?.QuerySelector("span")?.TextContent ?? "Фильмы").ToLower();

                        var itemSection = FilmixSiteProvider.Sections.FirstOrDefault(s => s.Title.ToLower() == sectionSpan);

                        if (itemSection == Section.Any)
                        {
                            return null;
                        }

#pragma warning disable CA2012 // Use ValueTasks correctly. Mirror will be read from cache.
                        var domain = siteProvider.GetMirrorAsync(default).GetAwaiter().GetResult();
#pragma warning restore CA2012 // Use ValueTasks correctly

                        return new ItemInfo(Site, id)
                        {
                            Link = tuple.jObject["link"]?.ToUriOrNull(domain) ?? new Uri(domain, $"drama/{id}-l.html"),
                            Section = itemSection,
                            Title = tuple.jObject["title"]?.ToString(),
                            Poster = FilmixSiteProvider.GetImage(domain, tuple.jObject["poster"]?.ToString()) ?? default,
                            Details =
                            {
                                TitleOrigin = tuple.jObject["original_name"]?.ToString(),
                                Year = tuple.jObject["year"]?.ToIntOrNull(),
                                Tags = new[]
                                {
                                    new TagsContainer(TagType.Genre,
                                        tuple.fragment?.Body?.TextContent?.Split(',').Select(c => new TitledTag(c.Trim())).ToArray() ?? Array.Empty<TitledTag>())
                                }
                            }
                        };
                    })
                    .Where(i => i != null && (section == Section.Any || section == i.Section))
                    .ToAsyncEnumerable())!;
        }

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return new[] { original };
            }

            var originalTitles = original.GetTitles();
            var originalTitlesTrimmed = originalTitles.Select(t => t.GetLettersAndDigits()).ToArray();
            return await originalTitles
                .ToAsyncEnumerable()
                .SelectMany(maska =>
                {
                    var pageParams = new SearchPageParams(
                        Site, Section.Any, DisplayItemMode.Minimal, 2, true, true, new Range(1902, DateTime.Now.Year + 1), tagsContainers, sortTypes.Keys);
                    var year = !original.Details.Year.HasValue ? (Range?)null : new Range(
                        original.Details.Year.Value - 1,
                        (original.Details.YearEnd ?? original.Details.Year).Value + 2);
                    var searchPage = new SearchPageFilter(pageParams!, maska)
                    {
                        Year = year
                    };

                    return GetShortResult(maska, Section.Any)
                        .GroupBy(_ => true)
                        .Concat(GetFullResult(searchPage)
                            .Take(IncrementalLoadingCollection.DefaultCount)
                            .GroupBy(_ => false))
                        .SelectAwaitWithCancellation((items, ct) => items.Select(i => (item: i, isShortResult: items.Key)).ToListAsync(ct));
                })
                .Select(list => list
                    .Where(tuple =>
                    {
                        if (tuple.item.Link == null)
                        {
                            return false;
                        }
                        if (!original.Details.Year.HasValue)
                        {
                            return true;
                        }
                        if (tuple.item.Details.Year == original.Details.Year)
                        {
                            return true;
                        }
                        if (tuple.item.Details.YearEnd.HasValue
                            && !original.Section.Modifier.HasFlag(SectionModifiers.Film)
                            && original.Details.Year >= tuple.item.Details.Year
                            && original.Details.Year <= tuple.item.Details.YearEnd)
                        {
                            return true;
                        }
                        return false;
                    })
                    .Select(tuple => (tuple.item, tuple.isShortResult, prox: tuple.item.GetTitles()
                        .Select(t => t.GetLettersAndDigits())
                        .MaxOrDefault(t => originalTitlesTrimmed.MaxOrDefault(c => c.Proximity(t)))))
                    .Where(tuple => tuple.prox > (tuple.isShortResult ? 0.95 : 0.85))
                    .OrderByDescending(tuple => tuple.prox)
                    .Select(tuple => tuple.item))
                .FirstOrDefaultAsync(items => items.Any(), cancellationToken)
                .ConfigureAwait(false)
                ?? Enumerable.Empty<ItemInfo>();
        }

        private async Task<(string, string)[]> FetchHiddenInputs()
        {
            var domain = await siteProvider.GetMirrorAsync(CancellationToken.None).ConfigureAwait(false);

            var response = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, "search"))
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .WithAjax()
                .SendAsync(CancellationToken.None)
                .ConfigureAwait(false);

            var fixingPage = await response
                .AsHtml(CancellationToken.None)
                .ConfigureAwait(false);

            if (response != null && fixingPage != null)
            {
                var newCookies = response.GetCookies().ToArray();
                siteProvider.Handler.SetCookies(domain, newCookies);

                return fixingPage
                    .QuerySelectorAll("form#fullsearch input[name][type='hidden']:not([disabled])")
                    .Select(input => (name: input.GetAttribute("name"), value: input.GetAttribute("value")))
                    .Where(tuple => !string.IsNullOrWhiteSpace(tuple.name) && !string.IsNullOrWhiteSpace(tuple.value))
                    .ToArray()!;
            }

            return Array.Empty<(string, string)>();
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            var request = filter.SearchRequest;
            var section = filter.PageParams.Section;
            var sortType = filter.CurrentSortType;
            var yearRange = filter.Year ?? filter.PageParams.YearLimit;
            var tags = filter.SelectedTags.ToArray();

            return Enumerable.Range(1, int.MaxValue)
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation(async (currentPage, ct) =>
                {
                    var domain = await siteProvider.GetMirrorAsync(ct).ConfigureAwait(false);
                    var referer = ("search/" + Uri.EscapeDataString(request)).ToUriOrNull(domain);

                    await missedHiddenArgs.Task.WaitAsync(ct).ConfigureAwait(false);
                    var hiddenArguments = missedHiddenArgs.Task.IsCompleted
                        ? await missedHiddenArgs
                        : Enumerable.Empty<(string, string)>();

                    var postArguments = GenerateArguments(request, currentPage, section, sortType, yearRange, tags);
                    var arguments = hiddenArguments.Concat(postArguments)
                        .GroupBy(a => a.Item1)
                        .Select(g => g.Last())
                        .ToDictionary(a => a.Item1, a => a.Item2);

                    var html = await siteProvider
                        .HttpClient
                        .PostBuilder(new Uri(domain, "engine/ajax/sphinx_search.php"))
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .WithBody(arguments)
                        .WithHeader("Origin", domain.GetOrigin())
                        .WithHeader("Referer", referer?.OriginalString)
                        .WithAjax()
                        .SendAsync(ct)
                        .AsHtml(ct)
                        .ConfigureAwait(false);
                    return (domain, html, elements: html?.QuerySelectorAll(".shortstory"));
                })
                .TakeWhile((tuple, index) =>
                {
                    if (tuple.html == null
                        || tuple.elements!.Length == 0)
                    {
                        return false;
                    }

                    var currentPage = index + 1;
                    var navPages = tuple.html.QuerySelectorAll(".navigation span")
                        .Select(s => int.TryParse(s.TextContent?.Trim(), out var t) ? t : -1)
                        .Where(p => p >= 0);

                    var maxPage = navPages.Any()
                        ? navPages.Max()
                        : currentPage;

                    return currentPage <= maxPage;
                })
                .SelectMany(tuple => tuple.elements
                   .Select(i => FilmixSiteProvider.ParseElement(tuple.domain, i))
                   .Where(i => i != null
                       && (section == Section.Any || section == i.Section))
                   .ToAsyncEnumerable())!;
        }

        private static IEnumerable<(string name, string value)> GenerateArguments(
            string request, int currentPage, Section section, SortType? sortType, Range? yearRange, TitledTag[] tags)
        {
            yield return ("story", request);
            yield return ("do", "search");
            yield return ("subaction", "search");

            if (sortType.HasValue)
            {
                yield return ("dle_sort_search", sortTypes[sortType.Value]);
                yield return ("dle_direction_search", sortType == SortType.Alphabet ? "asc" : "desc");
            }

            foreach (var tag in tags)
            {
                if (tag == default
                    || tag.Value == null)
                {
                    continue;
                }

                var valueId = tag.Value.GetDigits();
                switch (tag.Type)
                {
                    case "country":
                        yield return ("country_id[]", valueId);
                        break;
                    case "genre":
                        yield return ("ganre[]", valueId);
                        break;
                    case "q":
                        yield return ("rip[]", tag.Value);
                        break;
                }
            }

            if (yearRange.HasValue)
            {
                yield return ("years_ot", yearRange.Value.Start.Value.ToString());
                yield return ("years_do", (yearRange.Value.End.Value - 1).ToString());
            }

            if (section != default)
            {
                yield return ("vars", "1");
                if (section.Modifier.HasFlag(SectionModifiers.Cartoon))
                {
                    yield return (
                        section.Modifier.HasFlag(SectionModifiers.Serial) ? "multserials" : "multfilm",
                        "on");
                }
                else
                {
                    yield return (
                        section.Modifier.HasFlag(SectionModifiers.Serial) ? "serials" : "film",
                        "on");
                }
            }

            if (currentPage > 1)
            {
                yield return ("search_start", currentPage.ToString());
            }
            else
            {
                yield return ("search_start", "0");
            }
        }
    }
}

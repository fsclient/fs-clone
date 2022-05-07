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
    using FSClient.Shared.Providers;

    public class FilmixItemProvider : IItemProvider
    {
        private const string personFilterName = "person";

        private static readonly Dictionary<SortType, string> sortTypes = new Dictionary<SortType, string>
        {
            [SortType.UpdateDate] = "date",
            [SortType.Year] = "year",
            [SortType.Alphabet] = "title",
            [SortType.Commented] = "comm_num",
            [SortType.Visit] = "news_read",
            [SortType.Popularity] = "rating"
        };
        private static readonly TagsContainer[] titledTags = new TagsContainer[]
        {
            new TagsContainer(TagType.Genre, FilmixSiteProvider.Genres),
            new TagsContainer(TagType.County, FilmixSiteProvider.Countries),
            new TagsContainer(Strings.TagType_Quality, FilmixSiteProvider.Qualities)
        };

        private readonly FilmixSiteProvider siteProvider;

        public FilmixItemProvider(FilmixSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;

            Sections = FilmixSiteProvider.Sections;
        }

        public Site Site => siteProvider.Site;

        public IReadOnlyList<Section> Sections { get; }

        public bool HasHomePage => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public IAsyncEnumerable<ItemInfo> GetFullResult(SectionPageFilter filter)
        {
            var personLink = IsPersonRequest(filter.SelectedTags);
            var uncheckedRelativeLink = GenerateRelativeLink(filter.PageParams.Section, filter.SelectedTags, filter.Year);
            Uri? relativeLink = null;
            var sortType = filter.CurrentSortType ?? SortType.Year;
            var body = new Dictionary<string, string>
            {
                ["dlenewssortby"] = sortTypes[sortType],
                ["dledirection"] = sortType == SortType.Alphabet ? "asc" : "desc",
                ["set_new_sort"] = "dle_sort_cat",
                ["set_direction_sort"] = "dle_direction_cat"
            };

            return Enumerable.Range(1, int.MaxValue)
                .TakeWhile(currentPage => currentPage <= 1 || !personLink)
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation(async (currentPage, ct) =>
                {
                    var domain = await siteProvider.GetMirrorAsync(ct).ConfigureAwait(false);
                    siteProvider.Handler.SetCookie(domain, "per_page_news", "60");

                    if (relativeLink == null)
                    {
                        siteProvider.Handler.DeleteCookies(domain, "FILMIXNET");
                        var headResponse = await siteProvider.HttpClient
                            .HeadBuilder(new Uri(domain, uncheckedRelativeLink))
                            .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                            .WithBody(body)
                            .SendAsync(ct)
                            .ConfigureAwait(false);
                        relativeLink = headResponse?.Headers.Location ?? headResponse?.RequestMessage?.RequestUri;
                        if (relativeLink is null)
                        {
                            return default;
                        }
                    }

                    var link = relativeLink;

                    if (currentPage > 1)
                    {
                        link = new Uri(link, $"pages/{currentPage}");
                    }
                    if (!link.IsAbsoluteUri)
                    {
                        link = new Uri(domain, link);
                    }

                    var response = await siteProvider
                        .HttpClient
                        .PostBuilder(link)
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .WithBody(body)
                        .SendAsync(ct)
                        .ConfigureAwait(false);
                    if (response == null)
                    {
                        return default;
                    }

                    if (response.IsSuccessStatusCode != true
                        || (currentPage > 1
                            && !response.RequestMessage.RequestUri.ToString().Contains("page")))
                    {
                        return default;
                    }
                    return (domain, html: await response.AsHtml(ct).ConfigureAwait(false));
                })
                .TakeWhile((tuple, index) =>
                {
                    if (tuple.html == null)
                    {
                        return false;
                    }

                    var currentPage = index + 1;
                    var navPages = tuple.html.QuerySelectorAll(".navigation [data-number]")
                        .Select(s => int.TryParse(s.GetAttribute("data-number")?.Trim(), out var t) ? t : -1)
                        .Where(p => p >= 0);

                    var maxPage = navPages.Any()
                        ? navPages.Max()
                        : currentPage;

                    return currentPage <= maxPage;
                })
                .Select(tuple =>
                {
                    if (personLink)
                    {
                        return tuple.html!.QuerySelectorAll(".slider-item")
                            .Select(i =>
                            {
                                var itemLink = i.QuerySelector("a[href]")?.GetAttribute("href")?.ToUriOrNull(tuple.domain);
                                var id = FilmixSiteProvider.GetIdFromUrl(itemLink);
                                var title = i.QuerySelector(".film-name")?.TextContent?.Trim();
                                var poster = FilmixSiteProvider.GetImage(tuple.domain, i.QuerySelector("img[src]")?.GetAttribute("src"));
                                return new ItemInfo(siteProvider.Site, id)
                                {
                                    Link = itemLink,
                                    Poster = poster ?? default,
                                    Title = title
                                };
                            })
                            .Where(i => !string.IsNullOrEmpty(i?.SiteId))
                            .ToList();
                    }

                    return tuple.html!.QuerySelectorAll(".shortstory")
                        .Select(i => FilmixSiteProvider.ParseElement(tuple.domain, i))
                        .Where(i => i != null)
                        .ToList()!;
                })
                .TakeWhile(items => items.Count > 0)
                .SelectMany(items => items.ToAsyncEnumerable())!;
        }

        private static bool IsPersonRequest(IEnumerable<TitledTag> tags)
        {
            return tags.FirstOrDefault(t => t.Type == personFilterName) is TitledTag personTag
                && personTag != default;
        }

        private static Uri GenerateRelativeLink(Section section, IEnumerable<TitledTag> tags, Range? year)
        {
            if (tags.FirstOrDefault(t => t.Type == personFilterName) is TitledTag personTag
                && personTag != default)
            {
                return new Uri($"{personTag.Type}/{personTag.Value}", UriKind.Relative);
            }

            var filters = tags
                .Select(t => t.Type == "q" ? "q" + t.Value : t.Value)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (section.Modifier.HasFlag(SectionModifiers.Cartoon | SectionModifiers.Serial))
            {
                filters.Add("s93");
            }
            else if (section.Modifier.HasFlag(SectionModifiers.Cartoon))
            {
                filters.Add("s14");
            }
            else if (section.Modifier.HasFlag(SectionModifiers.Serial))
            {
                filters.Add("s7");
            }
            else if (section.Modifier.HasFlag(SectionModifiers.Film))
            {
                filters.Add("s999");
            }

            if (year.HasValue)
            {
                var yearTag = year.Value.HasRange()
                    ? $"r{year.Value.Start.Value}{year.Value.End.Value - 1}"
                    : $"y{year.Value.Start.Value}";
                filters.Add(yearTag);
            }

            return !filters.Any()
                ? new Uri($"/", UriKind.Relative)
                : new Uri($"filters/{string.Join("-", filters)}/", UriKind.Relative);
        }

        public ValueTask<SectionPageParams?> GetSectionPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            if (!Sections.Contains(section))
            {
                return default;
            }

            return new ValueTask<SectionPageParams?>(new SectionPageParams(
                Site, SectionPageType.Home, section, true, true, new Range(1902, DateTime.Now.Year + 1), titledTags, sortTypes.Keys));
        }

        public ValueTask<SectionPageParams?> GetSectionPageParamsForTagAsync(Section section, TitledTag titledTag, CancellationToken cancellationToken)
        {
            if (!Sections.Contains(section))
            {
                return default;
            }

            var isPersonTag = titledTag.Type == personFilterName;
            if (isPersonTag && section == Section.Any)
            {
                return new ValueTask<SectionPageParams?>(new SectionPageParams(
                    Site, SectionPageType.Tags, section, false, false));
            }
            else if (section != Section.Any)
            {
                return new ValueTask<SectionPageParams?>(new SectionPageParams(
                    Site, SectionPageType.Tags, section, true, true, new Range(1902, DateTime.Now.Year + 1), titledTags, sortTypes.Keys));
            }

            return default;
        }

        public async Task<HomePageModel?> GetHomePageModelAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            siteProvider.Handler.SetCookie(domain, "per_page_news", "45");

            var html = await siteProvider
                .HttpClient
                .GetBuilder(domain)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            if (html == null)
            {
                return null;
            }

            var topItems = html
                .QuerySelectorAll(".favorites-slider a")
                .Select(a =>
                {
                    if (!Uri.TryCreate(domain, a.GetAttribute("href"), out var link))
                    {
                        return null;
                    }

                    var id = FilmixSiteProvider.GetIdFromUrl(link);
                    if (id == null)
                    {
                        return null;
                    }

                    var img = a.QuerySelector("img");
                    var title = a.QuerySelector(".film-name")?.TextContent?.Trim();
                    if (title == "...")
                    {
                        title = img?.GetAttribute("title");
                    }

                    return new ItemInfo(Site, id)
                    {
                        Title = title,
                        Poster = FilmixSiteProvider.GetImage(domain, img?.GetAttribute("src")) ?? default,
                        Link = link
                    };
                })
                .Where(i => i != null)
                .ToList();

            var newItems = html
                .QuerySelectorAll(".content .shortstory")
                .Select(i => FilmixSiteProvider.ParseElement(domain, i))
                .Where(i => i != null)
                .GroupBy(_ => Strings.HomePageModel_New)
                .ToList();

            if (topItems.Count > 0 && newItems.Count > 0)
            {
                return new HomePageModel(Site, Strings.HomePageModel_Caption)
                {
                    TopItemsCaption = Strings.HomePageModel_Popular,
                    TopItems = topItems!,
                    HomeItems = newItems!
                };
            }
            return null;
        }
    }
}

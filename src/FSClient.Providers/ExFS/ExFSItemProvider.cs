namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Nito.AsyncEx;

    public sealed class ExFSItemProvider : IItemProvider, IDisposable
    {
        private static readonly int[] IngoredYears = {
            1916, 1918, 1919, 1920, 1922, 1923, 1924, 1929, 1930, 1932, 1934
        };

        private static readonly Dictionary<SortType, string> sortTypes = new Dictionary<SortType, string>
        {
            [SortType.UpdateDate] = "date",
            [SortType.Alphabet] = "title",
            [SortType.Rating] = "rating",
            [SortType.Visit] = "views",
            [SortType.Commented] = "comm",
            [SortType.Year] = "year",
            [SortType.KinoPoisk] = "kp_rating",
            [SortType.IMDb] = "imdb"
        };
        private static readonly TagsContainer[] tagsContainers = new[]
        {
            new TagsContainer(TagType.Genre, ExFSSiteProvider.Genres),
            new TagsContainer(TagType.County, ExFSSiteProvider.Countries),
            new TagsContainer(Strings.TagType_Quality, ExFSSiteProvider.Qualities)
        };

        private readonly Dictionary<string, string> lastTags = new Dictionary<string, string>
        {
            ["defaultsort"] = "",
            ["genre"] = "",
            ["year"] = "",
            ["country"] = "",
            ["quality"] = "",
            ["age_limit"] = ""
        };

        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        private readonly HttpClient httpClient = new HttpClient();

        private readonly ExFSSiteProvider siteProvider;

        public ExFSItemProvider(
            ExFSSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public IReadOnlyList<Section> Sections => ExFSSiteProvider.Sections;

        public Site Site => siteProvider.Site;

        public bool HasHomePage => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public ValueTask<SectionPageParams?> GetSectionPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            if (!Sections.Contains(section))
            {
                return default;
            }

            return new ValueTask<SectionPageParams?>(new SectionPageParams(
                Site, SectionPageType.Home, section, true, false, new Range(1915, DateTime.Now.Year + 1), tagsContainers, sortTypes.Keys));
        }

        public ValueTask<SectionPageParams?> GetSectionPageParamsForTagAsync(Section section, TitledTag titledTag, CancellationToken cancellationToken)
        {
            if (!Sections.Contains(section))
            {
                return default;
            }

            return new ValueTask<SectionPageParams?>(new SectionPageParams(
                Site, SectionPageType.Tags, section, true, false, new Range(1915, DateTime.Now.Year + 1), tagsContainers));
        }

        private static ItemInfo? GetFromDiv(Uri domain, IElement div)
        {
            var aTag = div.QuerySelector(".MiniPostName a, .MiniPostNameAct a");

            if (!Uri.TryCreate(
                domain,
                (aTag ?? div.QuerySelector(".MiniPostPoster, .MiniPostPosterSl, .MiniPostPosterAct"))?.GetAttribute("href"),
                out var link))
            {
                return null;
            }

            var id = ExFSSiteProvider.GetIdFromUrl(link);

            var img = div.QuerySelector("img");

            var title = aTag?.GetAttribute("title")
                        ?? aTag?.TextContent?.Trim()
                        ?? img?.GetAttribute("title")?
                            .Replace("Смотреть «", "")
                            .Replace("» онлайн", "");

            var section = ExFSSiteProvider.GetSectionFromUrl(link);

            var item = new ItemInfo(Sites.ExFS, id)
            {
                Link = link,
                Poster = ExFSSiteProvider.GetPoster(domain, img?.GetAttribute("src")) ?? default,
                Title = title,
                Section = section
            };

            if (!section.Modifier.HasFlag(SectionModifiers.Film))
            {
                var parts = div.QuerySelector(".customInfo")?
                    .TextContent?
                    .Split('s', 'e')
                    .Select(part => part.ToIntOrNull())
                    .Where(part => part.HasValue)
                    ?? Enumerable.Empty<int?>();
                item.Details.Status = new Status(
                    currentSeason: parts.Reverse().Skip(1).FirstOrDefault(),
                    currentEpisode: parts.LastOrDefault());
            }

            return item;
        }

        private async IAsyncEnumerable<ItemInfo> GetIncrementalContent(
            Uri originalLink, Section section, TitledTag[] tags, SortType sort,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int page = 0, maxPage = 1;
            var link = originalLink;
            Uri? domain = null;

            while (page < maxPage)
            {
                if (domain == null)
                {
                    domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
                    link = originalLink = new Uri(domain, originalLink);
                }

                var currentPage = ++page;
                link = currentPage > 1
                    ? new Uri(originalLink, $"page/{currentPage}/")
                    : link;

                if (tags.Any(tag => tag.Type == "year" && IngoredYears.Contains(int.Parse(tag.Value!))))
                {
                    yield break;
                }

                HttpResponseMessage? lastResponse = null;

                using (await semaphore.LockAsync(cancellationToken).ConfigureAwait(false))
                {
                    var newTags = GenerateNewTagsAndUpdateLast(tags, sort);

                    if (newTags.Count > 0)
                    {
                        foreach (var tag in newTags)
                        {
                            lastResponse = await httpClient
                                .PostBuilder(link)
                                .WithBody(new Dictionary<string, string>
                                {
                                    ["xsort"] = "1",
                                    ["xs_field"] = tag.Key,
                                    ["xs_value"] = tag.Value
                                })
                                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                                .SendAsync(cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }

                    lastResponse = await httpClient
                        .GetBuilder(link)
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .SendAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                if (lastResponse?.RequestMessage?.RequestUri?.ToString().Trim('/') != link.ToString().Trim('/'))
                {
                    yield break;
                }

                var html = await lastResponse.AsHtml(cancellationToken).ConfigureAwait(false);
                if (html == null)
                {
                    yield break;
                }

                var lastPageStr = html.QuerySelectorAll(".navigations a").LastOrDefault(a => int.TryParse(a.TextContent, out _))?.TextContent;
                maxPage = int.TryParse(lastPageStr, out var temp) ? temp : currentPage - 1;

                var items = html
                    .QuerySelectorAll(".MiniPostAllForm")
                    .Select(d => GetFromDiv(domain, d))
                    .Where(i => i != null && (section == Section.Any || section == i.Section));
                foreach (var item in items)
                {
                    yield return item!;
                }
            }
        }

        private Dictionary<string, string> GenerateNewTagsAndUpdateLast(TitledTag[] tags, SortType sort)
        {
            var newTags = tags
                .Concat(new[] { new TitledTag(Site, "defaultsort", sortTypes[sort]) })
                .Where(t => t.Type != null && t.Value != null)
                .GroupBy(t => t.Type)
                .Select(g => g.Last())
                .ToDictionary(t => t.Type!, t => t.Value!);

            foreach (var tagKey in lastTags.Keys.ToArray())
            {
                if (newTags.TryGetValue(tagKey, out var tagValue))
                {
                    if (tagValue == lastTags[tagKey])
                    {
                        newTags.Remove(tagKey);
                    }
                    else
                    {
                        lastTags[tagKey] = tagValue;
                    }
                }
                else if (!string.IsNullOrEmpty(lastTags[tagKey]))
                {
                    newTags.Add(tagKey, "");
                    lastTags[tagKey] = "";
                }
            }

            return newTags;
        }

        private async IAsyncEnumerable<ItemInfo> GetIncrementalContentLegacy(
            Uri link, Section section,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var page = 1;

            while (true)
            {
                var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
                link = new Uri(domain, link);

                var currentPage = page++;
                var l = currentPage > 1 ? new Uri(link + "page/" + currentPage + "/") : link;

                var resp = await siteProvider
                    .HttpClient
                    .GetBuilder(l)
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .SendAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (resp?.RequestMessage?.RequestUri?.ToString().Trim('/') != l.ToString().Trim('/'))
                {
                    yield break;
                }

                var items = ((await resp.AsHtml(cancellationToken).ConfigureAwait(false))?
                    .QuerySelectorAll(".MiniPostAllForm")
                    .Select(d => GetFromDiv(domain, d))
                    .Where(i => i != null && (section == Section.Any || section == i.Section))
                    .ToArray()
                    ?? Array.Empty<ItemInfo>());
                if (items.Length == 0)
                {
                    yield break;
                }
                foreach (var item in items)
                {
                    yield return item!;
                }
            }
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SectionPageFilter filter)
        {
            var tags = filter.SelectedTags.Where(t => t != TitledTag.Any);
            if (filter.Year.HasValue
                && !filter.Year.Equals(filter.PageParams.YearLimit))
            {
                var yearTag = new TitledTag(Site, "year", filter.Year.Value.Start.ToString());
                tags = tags.Concat(new[] { yearTag });
            }

            var oneTagModeRequired = tags.Any()
                && (filter.PageParams.Section == Section.Any
                    || tags.Any(t => !lastTags.ContainsKey(t.Type!)));

            if (oneTagModeRequired)
            {
                var tag = tags.OrderBy(t => lastTags.ContainsKey(t.Type!))
                    .FirstOrDefault();
                Uri.TryCreate($"/{tag.Type}/{tag.Value}/", UriKind.Relative, out var link);

                if (link != null)
                {
                    return GetIncrementalContentLegacy(link, filter.PageParams.Section);
                }
            }
            else
            {
                Uri.TryCreate($"/{filter.PageParams.Section.Value}/", UriKind.Relative, out var link);

                if (link != null)
                {
                    return GetIncrementalContent(link, filter.PageParams.Section, tags.ToArray(), filter.CurrentSortType ?? sortTypes.Keys.First());
                }
            }
            return AsyncEnumerable.Empty<ItemInfo>();
        }

        public void Dispose()
        {
            semaphore.Dispose();
            httpClient.Dispose();
        }

        public async Task<HomePageModel?> GetHomePageModelAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

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
                .QuerySelectorAll(".MiniPostSl")
                .Select(d => GetFromDiv(domain, d))
                .Where(i => i != null)
                .Take(10)
                .ToList();

            var homeItems = html
                .QuerySelectorAll(".MiniPostAllForm")
                .Select(d => GetFromDiv(domain, d))
                .Where(i => i != null)
                .GroupBy(i => i!.Section.Title)
                .ToList();

            if (topItems.Count > 0 && homeItems.Count > 0)
            {
                return new HomePageModel(Site, Strings.HomePageModel_Caption)
                {
                    TopItemsCaption = Strings.HomePageModel_Popular,
                    TopItems = topItems!,
                    HomeItems = homeItems!
                };
            }
            return null;
        }
    }
}

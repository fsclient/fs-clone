namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Newtonsoft.Json.Linq;

    public class TMDbItemProvider : IItemProvider
    {
        private static readonly Dictionary<SortType, string> sortTypes = new Dictionary<SortType, string>
        {
            [SortType.Year] = "release_date",
            [SortType.Popularity] = "popularity",
            [SortType.Rating] = "vote_average",
            [SortType.Revenue] = "revenue"
        };

        private static readonly Range DefaultYearRange = new Range(1902, DateTime.Now.Year + 1);

        private readonly TMDbSiteProvider siteProvider;
        private readonly TMDbItemInfoProvider itemInfoProvider;
        private readonly ICacheService cacheService;

        public TMDbItemProvider(
            TMDbSiteProvider siteProvider,
            TMDbItemInfoProvider itemInfoProvider,
            ICacheService cacheService)
        {
            this.siteProvider = siteProvider;
            this.itemInfoProvider = itemInfoProvider;
            this.cacheService = cacheService;
        }

        public Site Site => siteProvider.Site;

        public IReadOnlyList<Section> Sections => TMDbSiteProvider.Sections;

        public bool HasHomePage => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async ValueTask<SectionPageParams?> GetSectionPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            if (!Sections.Contains(section))
            {
                return null;
            }
            var genres = await siteProvider
                .FetchGenresAsync(cacheService, section.Modifier.HasFlag(SectionModifiers.Serial), cancellationToken)
                .ConfigureAwait(false);
            var tags = new TagsContainer(TagType.Genre, new[] { TitledTag.Any }.Concat(genres).ToArray());
            var currentSortTypes = section.Modifier.HasFlag(SectionModifiers.Serial)
                ? sortTypes.Keys.Except(new[] { SortType.Revenue }).ToArray()
                : sortTypes.Keys.ToArray();
            return new SectionPageParams(Site, SectionPageType.Home, section, true, true, DefaultYearRange, new[] { tags }, currentSortTypes);

        }

        public async ValueTask<SectionPageParams?> GetSectionPageParamsForTagAsync(Section section, TitledTag titledTag, CancellationToken cancellationToken)
        {
            if (!Sections.Contains(section))
            {
                return null;
            }

            var genres = await siteProvider
                .FetchGenresAsync(cacheService, section.Modifier.HasFlag(SectionModifiers.Serial), cancellationToken)
                .ConfigureAwait(false);
            var tags = new TagsContainer(TagType.Genre, new[] { TitledTag.Any }.Concat(genres).ToArray());
            var currentSortTypes = section.Modifier.HasFlag(SectionModifiers.Serial)
                ? sortTypes.Keys.Except(new[] { SortType.Revenue }).ToArray()
                : sortTypes.Keys.ToArray();
            return new SectionPageParams(Site, SectionPageType.Tags, section, true, true, DefaultYearRange, new[] { tags }, currentSortTypes);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SectionPageFilter filter)
        {
            var section = filter.PageParams.Section;

            var arguments = new Dictionary<string, string?>
            {
                ["vote_count.gte"] = "0.1"
            };

            if (filter.CurrentSortType is SortType sortType)
            {
                arguments.Add("sort_by", sortTypes[sortType] + ".desc");
            }

            if (filter.CurrentSortType == SortType.Rating)
            {
                var serial = filter.PageParams.Section.Modifier.HasFlag(SectionModifiers.Serial);
                var cartoon = filter.PageParams.Section.Modifier.HasFlag(SectionModifiers.Cartoon);

                arguments["vote_count.gte"] = serial && cartoon ? "50"
                    : serial ? "200"
                    : cartoon ? "1000"
                    : "3000";
            }

            var tags = filter.SelectedTags.Where(t => t.Type != null && TMDbSiteProvider.SupportedTagsForSearch.Contains(t.Type))
                .GroupBy(t => t.Type)
                .Select(g => g.First());

            foreach (var tag in tags)
            {
                arguments["with_" + tag.Type] = tag.Value;
            }

            if (!Settings.Instance.IncludeAdult
                && siteProvider.Properties.TryGetValue(TMDbSiteProvider.IgnoredKeywordsKey, out var ignoredKeywords))
            {
                arguments.Add("without_keywords", ignoredKeywords);
            }

            if (section.Modifier.HasFlag(SectionModifiers.Cartoon))
            {
                if (arguments.TryGetValue("with_genres", out var genres))
                {
                    if (genres != TMDbSiteProvider.CartoonId)
                    {
                        arguments["with_genres"] = genres + "," + TMDbSiteProvider.CartoonId;
                    }
                }
                else
                {
                    arguments.Add("with_genres", TMDbSiteProvider.CartoonId);
                }
            }
            else
            {
                if (arguments.TryGetValue("with_genres", out var genres)
                    && genres == TMDbSiteProvider.CartoonId)
                {
                    arguments.Remove("with_genres");
                }

                arguments.Add("without_genres", TMDbSiteProvider.CartoonId);
            }

            if ((filter.Year ?? DefaultYearRange) is Range years)
            {
                var nowYear = DateTime.UtcNow;
                arguments.Add(
                    section.Modifier.HasFlag(SectionModifiers.Serial) ? "first_air_date.gte" : "release_date.gte",
                    new DateTime(years.Start.Value, 1, 1).ToString("yyyy-MM-dd"));
                arguments.Add(
                    section.Modifier.HasFlag(SectionModifiers.Serial) ? "first_air_date.lte" : "release_date.lte",
                    (nowYear.Year == (years.End.Value - 1) ? nowYear : new DateTime(years.End.Value, 1, 1)).ToString("yyyy-MM-dd"));
                if (section.Modifier.HasFlag(SectionModifiers.Serial))
                {
                    arguments.Add("include_null_first_air_dates", "false");
                }
            }

            var sectionName = section.Modifier.HasFlag(SectionModifiers.Serial) ? "tv" : "movie";

            return Enumerable.Range(1, int.MaxValue)
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((currentPage, ct) =>
                {
                    var argumentsWithPage = new[] { new KeyValuePair<string, string?>("page", currentPage.ToString()) }
                        .Concat(arguments);

                    return new ValueTask<JObject?>(siteProvider.GetFromApiAsync($"discover/{sectionName}", argumentsWithPage, ct)
                        .AsNewtonsoftJson<JObject>());
                })
                .TakeWhile((json, index) =>
                {
                    if (json?["results"] == null)
                    {
                        return false;
                    }
                    var maxPages = json["total_pages"]?.ToIntOrNull() ?? int.MaxValue;
                    var currentPage = json["page"]?.ToIntOrNull() ?? index + 1;
                    return currentPage <= maxPages;
                })
                .SelectAwaitWithCancellation((json, ct) => json!["results"]
                    .OfType<JObject>()
                    .ToAsyncEnumerable()
                    .SelectAwaitWithCancellation(itemInfoProvider.CreateItemInfoFromJsonAsync)
                    .Where(i => i?.Section.Modifier.HasFlag(SectionModifiers.Cartoon) == section.Modifier.HasFlag(SectionModifiers.Cartoon))
                    .ToArrayAsync(ct))
                .TakeWhile(items => items.Length > 0)
                .SelectMany(items => items.ToAsyncEnumerable())
                .Where(i => i != null && i.Poster.Count > 0)!;
        }

        public Task<HomePageModel?> GetHomePageModelAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<HomePageModel?>(null);
        }
    }
}

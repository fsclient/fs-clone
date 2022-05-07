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

    public class ShikiSearchProvider : ShikiBaseSearchProvider, ISearchProvider
    {
        private const char HiraganaStart = '\u3041';
        private const char HiraganaEnd = '\u3096';
        private const char KatakanaStart = '\u30A1';
        private const char KatakanaEnd = '\u30FC';
        private const char KanjiStart = '\u4E00';
        private const char KanjiEnd = '\u9FAF';

        private readonly ShikiSiteProvider siteProvider;
        private readonly ICacheService? cacheService;

        public ShikiSearchProvider(
            ShikiSiteProvider siteProvider,
            ICacheService? cacheService)
            : base(siteProvider)
        {
            this.siteProvider = siteProvider;
            this.cacheService = cacheService;
        }

        public Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return EnumerableHelper
                    .ToAsyncEnumerable(ct => siteProvider.EnsureItemAsync(original, ct).AsTask(), cancellationToken)
                    .ToEnumerableAsync(cancellationToken);
            }

            if (original.Details.TitleOrigin == null
                || !original.Section.Modifier.HasFlag(SectionModifiers.Cartoon))
            {
                return Task.FromResult(Enumerable.Empty<ItemInfo>());
            }

            var titles = original
                .GetTitles()
                .ToArray();

            var onlyJp = titles.All(t => t.Any(c =>
                (c >= HiraganaStart && c <= HiraganaEnd)
                || (c >= KatakanaStart && c <= KatakanaEnd)
                || (c >= KanjiStart && c <= KanjiEnd)));

            return titles
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((title, ct) => new ValueTask<JArray?>(siteProvider
                    .GetFromApiAsync(
                        "/api/animes",
                        new Dictionary<string, string>
                        {
                            ["search"] = title,
                            ["season"] = original.Details.Year?.ToString() ?? string.Empty,
                            ["limit"] = "10",
                            ["censored"] = Settings.Instance.IncludeAdult ? "false" : "true"
                        },
                        ct)
                    .AsNewtonsoftJson<JArray>()))
                .TakeWhile(jArray => jArray != null)
                .SelectMany(jArray => jArray!
                    .OfType<JObject>()
                    .Where(jsonMain => jsonMain != null)
                    .Where(i => i != null && CheckMain(i, original.Section.Modifier))
                    .Select(i => (item: i, w: GetWeight(i, titles)))
                    .OrderByDescending(t => t.w)
                    // Shikimori doesn't return japanese name in search, so can't filter by it
                    .Where(t => t.w > 0.9 || onlyJp)
                    .Select(t => t.item)
                    .Select((jsonItem) =>
                    {
                        var item = new ItemInfo(siteProvider.Site, jsonItem["id"]?.ToString());
#pragma warning disable CA2012 // Use ValueTasks correctly. I will always read from cache.
                        var domain = siteProvider.GetMirrorAsync(default).GetAwaiter().GetResult();
#pragma warning restore CA2012 // Use ValueTasks correctly
                        var result = ShikiItemInfoProvider.UpdateItemInfoFromAnimesApiJson(domain, item, jsonItem);
                        return result ? item : null;
                    })
                    .Where(item => item?.SiteId != null
                        && (!item.Details.Year.HasValue
                        || !original.Details.Year.HasValue
                        || item.Details.Year == original.Details.Year))
                    .ToAsyncEnumerable())
                // Simplify by usage
                .Take(1)
                .ToEnumerableAsync(cancellationToken)!;

            static double GetWeight(JObject json, string[] titles)
            {
                double w = 0;

                if (json["russian"] is JToken ruProperty
                    && ruProperty.Type == JTokenType.String)
                {
                    w = titles.Select(t => t.Proximity(ruProperty.ToString())).Max();
                }

                if (json["name"] is JToken orProperty
                    && orProperty.Type == JTokenType.String)
                {
                    w = Math.Max(w, titles.Select(t => t.Proximity(orProperty.ToString())).Max());
                }

                return w;
            }

            static bool CheckMain(JObject json, SectionModifiers sectionModifier)
            {
                if (json["status"]?.ToString() == "anons")
                {
                    return false;
                }

                var kind = json["kind"]?.ToString();

                if (sectionModifier.HasFlag(SectionModifiers.Film))
                {
                    return kind == "movie"
                           || ((kind == "ova" || kind == "special")
                               && json["episodes"]?.ToIntOrNull() == 1);
                }

                if (sectionModifier.HasFlag(SectionModifiers.Serial))
                {
                    return kind == "tv"
                           || ((kind == "ova" || kind == "special")
                               && json["episodes"]?.ToIntOrNull() > 1);
                }

                return true;
            }
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter.SearchRequest, filter.PageParams.Section, filter.Year, filter.SelectedTags.ToList(), filter.CurrentSortType);
        }

        public async ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            var genres = cacheService == null
                ? await FetchGenresFromApiAsync(string.Empty, cancellationToken)
                .ConfigureAwait(false)
                : await cacheService.GetOrAddAsync($"{Site.Value}_Genres", FetchGenresFromApiAsync, TimeSpan.FromDays(30), cancellationToken)
                .ConfigureAwait(false);
            return new SearchPageParams(Site, section, DisplayItemMode.Normal, 2, true, true, new Range(1917, DateTime.Today.Year + 1), GetTagsContainers(genres, section), GetSortTypes(section));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResultInternal(request, section, null, new List<TitledTag>(), null);
        }
    }
}

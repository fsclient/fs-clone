namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Humanizer;

    using Newtonsoft.Json.Linq;

    public abstract class ShikiBaseSearchProvider
    {
        private static readonly Dictionary<SortType, string> sortTypes = new Dictionary<SortType, string>
        {
            [SortType.Popularity] = "popularity",
            [SortType.Alphabet] = "name",
            [SortType.Year] = "aired_on",
            [SortType.Random] = "random",
            [SortType.Rating] = "ranked",
            [SortType.Episodes] = "episodes"
            //[] = "kind",
            //[] = "status"
        };

        private static readonly TagsContainer[] commonContainers = new[]
        {
            new TagsContainer("Рейтинг",
                TitledTag.Any,
                new TitledTag("Без рейтинга", ShikiSiteProvider.SiteKey, "rating", "none"),
                new TitledTag("G", ShikiSiteProvider.SiteKey, "rating", "g"),
                new TitledTag("PG", ShikiSiteProvider.SiteKey, "rating", "pg"),
                new TitledTag("PG 13", ShikiSiteProvider.SiteKey, "rating", "pg_13"),
                new TitledTag("R", ShikiSiteProvider.SiteKey, "rating", "r"),
                new TitledTag("R+", ShikiSiteProvider.SiteKey, "rating", "r_plus"),
                new TitledTag("Rx", ShikiSiteProvider.SiteKey, "rating", "rx")),
            new TagsContainer("Статус",
                TitledTag.Any,
                new TitledTag("Анонс", ShikiSiteProvider.SiteKey, "status", "anons"),
                new TitledTag("Онгоинг", ShikiSiteProvider.SiteKey, "status", "ongoing"),
                new TitledTag("Вышло", ShikiSiteProvider.SiteKey, "status", "released")),
            new TagsContainer("Оценка",
                TitledTag.Any,
                new TitledTag("9+", ShikiSiteProvider.SiteKey, "score", "9"),
                new TitledTag("8+", ShikiSiteProvider.SiteKey, "score", "8"),
                new TitledTag("7+", ShikiSiteProvider.SiteKey, "score", "7"),
                new TitledTag("6+", ShikiSiteProvider.SiteKey, "score", "6"),
                new TitledTag("5+", ShikiSiteProvider.SiteKey, "score", "5")),
            new TagsContainer("Длительность",
                TitledTag.Any,
                new TitledTag("До 10 минут", ShikiSiteProvider.SiteKey, "duration", "S"),
                new TitledTag("До 30 минут", ShikiSiteProvider.SiteKey, "duration", "D"),
                new TitledTag("Дольше 30 минут", ShikiSiteProvider.SiteKey, "duration", "F"))
        };

        private static readonly TagsContainer[] serialTagsContainers = new[]
        {
            new TagsContainer("Число серий",
                TitledTag.Any,
                new TitledTag("До 16 серий", ShikiSiteProvider.SiteKey, "kind", "tv_13"),
                new TitledTag("До 28 серий", ShikiSiteProvider.SiteKey, "kind", "tv_24"),
                new TitledTag("Больше 28 серий", ShikiSiteProvider.SiteKey, "kind", "tv_48"))
        };

        private readonly ShikiSiteProvider siteProvider;

        public ShikiBaseSearchProvider(
            ShikiSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            Sections = new[] { Section.Any }.Concat(ShikiSiteProvider.Sections).ToList();
        }

        public virtual IReadOnlyList<Section> Sections { get; }

        public virtual Site Site => siteProvider.Site;

        public virtual ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            string? searchRequest, Section section, Range? year, List<TitledTag> tags, SortType? sortType)
        {
            if (!tags.Any(t => t.Type == "kind")
                && section != Section.Any)
            {
                tags.Add(new TitledTag(siteProvider.Site, "kind", section.Value));
            }

            if (year.HasValue)
            {
                if (year.Value.HasRange())
                {
                    tags.Add(new TitledTag(siteProvider.Site, "season", $"{year.Value.Start.Value}_{year.Value.End.Value}"));
                }
                else
                {
                    tags.Add(new TitledTag(siteProvider.Site, "season", $"{year.Value.Start.Value}"));
                }
            }
            if (sortType.HasValue)
            {
                tags.Add(new TitledTag(siteProvider.Site, "order", sortTypes[sortType.Value]));
            }

            tags.Add(new TitledTag(siteProvider.Site, "limit", "40"));
            if (!string.IsNullOrWhiteSpace(searchRequest))
            {
                tags.Add(new TitledTag(siteProvider.Site, "search", searchRequest));
            }

            if (!tags.Any(tag => tag.Value == "anons"))
            {
                tags.Add(new TitledTag(siteProvider.Site, "status", "!anons"));
            }

            return Enumerable.Range(1, int.MaxValue)
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((currentPage, ct) => new ValueTask<JArray?>(siteProvider
                    .GetFromApiAsync(
                        "/api/animes",
                        tags
                            .Concat(new[] { new TitledTag(siteProvider.Site, "page", currentPage.ToString()) })
                            .GroupBy(t => t.Type!)
                            .ToDictionary(t => t.Key, t => string.Join(",", t.Select(tag => tag.Value))),
                        ct)
                    .AsNewtonsoftJson<JArray>()))
                .TakeWhile(json => json != null)
                .SelectAwaitWithCancellation((json, ct) => json
                    .OfType<JObject>()
                    .ToAsyncEnumerable()
                    .Select(jsonItem =>
                    {
                        var item = new ItemInfo(siteProvider.Site, jsonItem["id"]?.ToString());
#pragma warning disable CA2012 // Use ValueTasks correctly. I will always read from cache.
                        var domain = siteProvider.GetMirrorAsync(ct).GetAwaiter().GetResult();
#pragma warning restore CA2012 // Use ValueTasks correctly
                        var result = ShikiItemInfoProvider.UpdateItemInfoFromAnimesApiJson(domain, item, jsonItem);
                        return result ? item : null;
                    })
                    .Where(item => item != null)
                    .ToListAsync(ct))
                .TakeWhile(items => items.Count > 0)
                .SelectMany(items => items.ToAsyncEnumerable())!;
        }

        protected Task<TitledTag[]> FetchGenresFromApiAsync(string _, CancellationToken cancellationToken)
        {
            return siteProvider
                .GetFromApiAsync("/api/genres", cancellationToken)
                .AsNewtonsoftJson<JArray>()
                .ToAsyncEnumerable()
                .TakeWhile(json => json != null)
                .SelectMany(json => json!.ToAsyncEnumerable())
                .Where(json => json["kind"]?.ToString() == "anime")
                .Select(json => new TitledTag(
                    (json["russian"] ?? json["name"])?.ToString().Transform(To.TitleCase) ?? "unknown",
                    Site,
                    "genre",
                    json["id"]?.ToString() ?? ""))
                .OrderBy(t => t.Value == "27" || t.Value == "28" || t.Value == "42" ? 0
                    : t.Value == "25" || t.Value == "26" || t.Value == "43" ? 1
                    : t.Value == "4" || t.Value == "22" || t.Value == "23" ? 2
                    : t.Value == "12" || t.Value == "33" || t.Value == "34" ? 4 : 3)
                .ThenBy(t => t.Title)
                .ToArrayAsync(cancellationToken)
                .AsTask();
        }

        protected static IEnumerable<TagsContainer> GetTagsContainers(IEnumerable<TitledTag> genres, Section section)
        {
            yield return new TagsContainer(TagType.Genre, new[] { TitledTag.Any }.Concat(genres).ToArray());
            foreach (var container in commonContainers)
            {
                yield return container;
            }
            if (section.Modifier.HasFlag(SectionModifiers.Serial))
            {
                foreach (var container in serialTagsContainers)
                {
                    yield return container;
                }
            }
        }

        protected static IEnumerable<SortType> GetSortTypes(Section section)
        {
            if (section.Modifier.HasFlag(SectionModifiers.Serial))
            {
                return sortTypes.Keys;
            }
            else
            {
                return sortTypes.Keys.Where(s => s != SortType.Episodes);
            }
        }
    }
}

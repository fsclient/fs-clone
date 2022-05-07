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

    using Newtonsoft.Json.Linq;

    public class TMDbSearchProvider : ISearchProvider
    {
        private readonly TMDbSiteProvider siteProvider;
        private readonly TMDbItemInfoProvider itemInfoProvider;

        public TMDbSearchProvider(
            TMDbSiteProvider siteProvider,
            TMDbItemInfoProvider itemInfoProvider)
        {
            this.siteProvider = siteProvider;
            this.itemInfoProvider = itemInfoProvider;
        }

        public Site Site => siteProvider.Site;

        public IReadOnlyList<Section> Sections => TMDbSiteProvider.Sections;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResultInternal(request, section, null);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            var yearsRange = section != Section.Any
                ? new Range(1902, DateTime.Now.Year + 1)
                : (Range?)null;
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section, DisplayItemMode.Detailed, 3, false, false, yearsRange));
        }

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original == null)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            if (original.Site == Site)
            {
                return new[] { original };
            }

            var section = original.Section.Modifier.HasFlag(SectionModifiers.Serial) ? "tv" : "movie";

            if (original.Details.LinkedIds != null
                && original.Details.LinkedIds.TryGetValue(siteProvider.Site, out var tmdbId))
            {
                var json = await siteProvider
                    .GetFromApiAsync($"{section}/{tmdbId}", cancellationToken)
                    .AsNewtonsoftJson<JObject>()
                    .ConfigureAwait(false);

                if (json != null)
                {
                    var item = await itemInfoProvider.CreateItemInfoFromJsonAsync(json, cancellationToken).ConfigureAwait(false);

                    if (item != null)
                    {
                        return new[] { item };
                    }
                }
            }

            var titlesToProx = original.GetTitles().ToList();

            var copyDict = original.Details.LinkedIds.ToList();
            if (copyDict.OrderByDescending(p => p.Key == Sites.IMDb)
                    .FirstOrDefault(p => p.Key == Sites.IMDb || p.Key == Sites.Twitter) is { } pair
                && pair.Value != null
                && copyDict.FirstOrDefault(k => k.Key == Sites.IMDb).Value is string id)
            {
                var json = await siteProvider
                    .GetFromApiAsync(
                        $"find/{id}",
                        new Dictionary<string, string?>
                        {
                            ["external_source"] = pair.Key == Sites.IMDb ? "imdb_id"
                                : pair.Key == Sites.Twitter ? "twitter_id"
                                : throw new NotSupportedException("External source is not supported")
                        },
                        cancellationToken)
                    .AsNewtonsoftJson<JObject>()
                    .ConfigureAwait(false);

                var tmdbItems = await (json?["tv_results"] as JArray ?? new JArray())
                    .Concat(json?["movie_results"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .ToAsyncEnumerable()
                    .SelectAwaitWithCancellation(itemInfoProvider.CreateItemInfoFromJsonAsync)
                    .Where(item => item?.SiteId != null)
                    .OrderByDescending(item => Math.Max(
                        titlesToProx.Select(t => item!.Title?.Proximity(t, false) ?? 0).Max(),
                        titlesToProx.Select(t => item!.Details.TitleOrigin?.Proximity(t, false) ?? 0).Max()))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (tmdbItems.Count > 0)
                {
                    return tmdbItems!;
                }
            }

            if (original.GetTitles(true).FirstOrDefault() is not string title)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            var jsonSearch = await siteProvider
                .GetFromApiAsync(
                    $"search/{section}",
                    new Dictionary<string, string?>
                    {
                        ["query"] = title,
                        ["primary_release_year"] = original.Details.Year?.ToString() ?? ""
                    },
                    cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            return (await (jsonSearch?["results"] as JArray ?? new JArray())
                .OfType<JObject>()
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation(itemInfoProvider.CreateItemInfoFromJsonAsync)
                .Where(item => item?.SiteId != null)
                .Select(item => (item: item, prox: Math.Max(
                    titlesToProx.Select(t => item!.Title?.Proximity(t, false) ?? 0).Max(),
                    titlesToProx.Select(t => item!.Details.TitleOrigin?.Proximity(t, false) ?? 0).Max())))
                .Where(tuple => tuple.prox >= 0.8)
                .OrderByDescending(tuple => tuple.prox)
                .Select(tuple => tuple.item)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))!;
        }

        public async Task<ItemInfo?> FindSingleSimilarWithLinkedIdsAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Details.LinkedIds.ContainsKey(Sites.IMDb))
            {
                return original;
            }

            var similarItems = await FindSimilarAsync(original, cancellationToken).ConfigureAwait(false);
            var similar = similarItems.FirstOrDefault();
            if (similar == null)
            {
                return null;
            }

            var section = original.Section.Modifier.HasFlag(SectionModifiers.Serial) ? "tv" : "movie";
            var jsonSearch = await siteProvider
                .GetFromApiAsync(
                    $"{section}/{similar.SiteId}/external_ids",
                    cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            if (jsonSearch != null)
            {
                foreach (var (site, id) in TMDbItemInfoProvider.ParseExternalIds(jsonSearch))
                {
                    similar.Details.LinkedIds[site] = id;
                }
            }

            return similar;
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter.SearchRequest, filter.PageParams.Section, filter.Year?.Start.Value);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResultInternal(string request, Section section, int? year)
        {
            var sectionName = section.Modifier.HasFlag(SectionModifiers.Serial) ? "tv" : "movie";
            var url = section == Section.Any ? "search/multi" : $"search/{sectionName}";
            var adultCount = 0;
            const int maxAdultCount = 10;

            return Enumerable.Range(1, int.MaxValue)
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((currentPage, ct) =>
                {
                    var arguments = new Dictionary<string, string?>
                    {
                        ["query"] = request,
                        ["page"] = currentPage.ToString()
                    };
                    if (year is int exactYear)
                    {
                        arguments.Add(section.Modifier.HasFlag(SectionModifiers.Serial) ? "first_air_date_year" : "year", exactYear.ToString());
                    }

                    return new ValueTask<JObject?>(siteProvider
                        .GetFromApiAsync(url, arguments, ct)
                        .AsNewtonsoftJson<JObject>());
                })
                .TakeWhile((json, index) =>
                {
                    if (!(json?["results"] is JArray))
                    {
                        return false;
                    }
                    var maxPages = json["total_pages"]?.ToIntOrNull() ?? int.MaxValue;
                    var currentPage = json["page"]?.ToIntOrNull() ?? index + 1;
                    return currentPage <= maxPages;
                })
                .SelectAwaitWithCancellation((json, ct) => json!["results"]
                    .OfType<JObject>()
                    .Where(j => section != Section.Any || "tv,movie".Contains(j["media_type"]?.ToString() ?? ""))
                    .ToAsyncEnumerable()
                    .SelectAwaitWithCancellation(itemInfoProvider.CreateItemInfoFromJsonAsync)
                    .Where(item => item != null)
                    .DistinctBy(item => item!.SiteId)
                    .ToArrayAsync(ct))
                .TakeWhile(items => items.Length > 0 && adultCount < maxAdultCount)
                .SelectMany(items =>
                {
                    if (section != Section.Any)
                    {
                        return items.Where(item => item?.Section == section).ToAsyncEnumerable();
                    }
                    return items.ToAsyncEnumerable();
                })
                .Where(item => item!.Poster.Count > 0)
                .Take(50)
                .WhereAwaitWithCancellation(async (item, ct) =>
                {
                    if (Settings.Instance.IncludeAdult)
                    {
                        return true;
                    }

                    var isAdult = await itemInfoProvider.IsAdultAsync(item!, ct).ConfigureAwait(false);
                    if (isAdult)
                    {
                        adultCount++;
                        return false;
                    }
                    return true;
                })!;
        }
    }
}

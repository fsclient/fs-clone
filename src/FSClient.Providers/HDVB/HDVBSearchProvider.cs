namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class HDVBSearchProvider : ISearchProvider
    {
        private readonly HDVBSiteProvider siteProvider;

        public HDVBSearchProvider(HDVBSiteProvider hdvbSiteProvider)
        {
            siteProvider = hdvbSiteProvider;
        }

        public bool IgnoreBlocked { get; set; } = true;

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.CreateDefault(SectionModifiers.Film),
            Section.CreateDefault(SectionModifiers.Serial)
        };

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site && original.Link != null && original is HDVBItemInfo info)
            {
                return new[] { original };
            }

            if (original.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpIdStr)
                && int.TryParse(kpIdStr, out var kpId))
            {
                return await GetFullResult(original.Title ?? string.Empty, original.Section, kpId, null, null, cancellationToken)
                    .Take(IncrementalLoadingCollection.DefaultCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return await original
                .GetTitles()
                .Select(title => new Func<CancellationToken, Task<IEnumerable<ItemInfo>>>(
                    async ct => await GetFullResult(title, original.Section, null, original.Details.Status.CurrentSeason, original.Details.Year, ct)
                        .Take(IncrementalLoadingCollection.DefaultCount)
                        .ToListAsync(ct)
                        .ConfigureAwait(false)))
                .WhenAny(item => item?.Any() == true, Enumerable.Empty<ItemInfo>(), token: cancellationToken)
                .ConfigureAwait(false);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResult(filter.SearchRequest, filter.PageParams.Section, null, null, null);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResult(request, section, null, null, null);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResult(
            string searchRequest, Section section, int? kpIdFilter, int? maxSeason, int? minYear,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var args = new Dictionary<string, string?>
            {
                ["token"] = Secrets.HDVBApiKey
            };
            if (kpIdFilter.HasValue)
            {
                args.Add("id_kp", kpIdFilter.ToString());
            }
            else
            {
                args.Add("title", searchRequest);
            }

            var results = await siteProvider.HttpClient
                .GetBuilder(new Uri(domain, "api/videos.json"))
                .WithArguments(args)
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JArray>()
                .ConfigureAwait(false);

            if (results == null)
            {
                yield break;
            }

            var items = results
                .OfType<JObject>()
                .Select(item =>
                {
                    var itemInfo = HDVBItemInfoProvider.GetItemFromJObject(siteProvider.Site, domain, item);

                    var addedAtYear = item["added_date"]?.ToString().Split('-').First().ToIntOrNull();
                    var block = item["block"]?.ToBoolOrNull() ?? false;

                    if (block && IgnoreBlocked)
                    {
                        return default;
                    }

                    if (minYear.HasValue)
                    {
                        if (itemInfo.Details.Year is int year)
                        {
                            if (year != minYear)
                            {
                                return default;
                            }
                        }
                        else if (addedAtYear is int addedYear
                            && addedYear > 2000
                            && addedYear < minYear)
                        {
                            return default;
                        }
                    }

                    if (itemInfo.Details.Status.CurrentSeason is int season
                        && maxSeason.HasValue
                        // 1 for threshold
                        && Math.Abs(season - maxSeason.Value) > 1)
                    {
                        return default;
                    }

                    var prox = searchRequest == null ? 0
                        : Math.Max(
                            itemInfo.Title?.Proximity(searchRequest, false) ?? 0,
                            itemInfo.Details.TitleOrigin?.Proximity(searchRequest, false) ?? 0);

                    if (kpIdFilter.HasValue
                        && itemInfo.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kinopoiskId))
                    {
                        if (kpIdFilter != int.Parse(kinopoiskId))
                        {
                            return default;
                        }
                    }
                    else if (prox < 0.9)
                    {
                        return default;
                    }

                    return (prox, item: itemInfo);
                })
                .Where(t => t.item?.SiteId != null && t.item.Link != null
                    && t.item.Section.Modifier.HasFlag(section.Modifier))
                .OrderBy(t => t.prox)
                .GroupBy(t => t.item.SiteId, t => t.item)
                .Select(group => group
                    .OrderBy(item => string.IsNullOrEmpty(item.Translate))
                    .ThenBy(item => string.IsNullOrEmpty(item.Title))
                    .ThenBy(item => !item.Details.LinkedIds.ContainsKey(Sites.Kinopoisk))
                    .First())
                .Cast<ItemInfo>();

            foreach (var item in items)
            {
                yield return item;
            }
        }
    }
}

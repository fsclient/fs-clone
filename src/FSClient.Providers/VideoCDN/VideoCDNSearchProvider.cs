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

    public class VideoCDNSearchProvider : ISearchProvider
    {
        private readonly VideoCDNSiteProvider siteProvider;

        public VideoCDNSearchProvider(VideoCDNSiteProvider videoCDNSiteProvider)
        {
            siteProvider = videoCDNSiteProvider;
        }

        public IReadOnlyList<Section> Sections => siteProvider.Sections;

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site && original.Link != null)
            {
                return new[] { original };
            }

            var section = NormalizeSection(original.Section);

            if (original.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpIdStr)
                && int.TryParse(kpIdStr, out var kpId))
            {
                return await GetFullResultInternal(null, section, null, kpIdFilter: kpId, cancellationToken: cancellationToken)
                    .Take(IncrementalLoadingCollection.DefaultCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            if (original.Details.LinkedIds.TryGetValue(Sites.IMDb, out var imdbIdStr))
            {
                return await GetFullResultInternal(null, section, null, imdbIdFilter: imdbIdStr, cancellationToken: cancellationToken)
                    .Take(IncrementalLoadingCollection.DefaultCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return await original
                .GetTitles()
                .Select(title => new Func<CancellationToken, Task<IEnumerable<ItemInfo>>>(ct =>
                    GetFullResultInternal(title, section, original.Details.Year, cancellationToken: ct)
                        .Take(IncrementalLoadingCollection.DefaultCount)
                        .ToEnumerableAsync(cancellationToken)))
                .WhenAny(item => item?.Any() == true, Enumerable.Empty<ItemInfo>(), token: cancellationToken)
                .ConfigureAwait(false);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter.SearchRequest, filter.PageParams.Section, filter.Year?.Start.Value);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, NormalizeSection(section)));
        }

        private Section NormalizeSection(Section section)
        {
            var videoCdnSection = Sections.FirstOrDefault(s => s == section || s.Modifier == section.Modifier);
            if (videoCdnSection == Section.Any
                && section.Modifier.HasFlag(SectionModifiers.Cartoon))
            {
                // VideoCDN doesn't have Cartoon section, but we can use 'Film' for cartoon films and 'Serial' for cartoon serials
                var cleanSectionModifier = section.Modifier ^ SectionModifiers.Cartoon;
                videoCdnSection = Sections.FirstOrDefault(s => s.Modifier == cleanSectionModifier);
            }
            return videoCdnSection;
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetFullResultInternal(request, NormalizeSection(section));
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            string? searchRequest, Section section, int? yearFilter = null, int? kpIdFilter = null, string? imdbIdFilter = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int page = 0, maxPage = 1;

            var kindName = section.Value;

            while (page < maxPage)
            {
                var currentPage = ++page;

                var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

                var shortSearch = section == Section.Any || kindName == null;
                var args = new Dictionary<string, string?>
                {
                    ["api_token"] = Secrets.VideoCDNApiKey,
                    ["page"] = page.ToString(),
                    ["limit"] = "60"
                };

                if (shortSearch)
                {
                    if (kpIdFilter.HasValue)
                    {
                        args.Add("kinopoisk_id", kpIdFilter.Value.ToString());
                    }
                    else if (imdbIdFilter != null)
                    {
                        args.Add("imdb_id", imdbIdFilter);
                    }
                    else
                    {
                        args.Add("title", searchRequest);
                    }
                }
                else
                {
                    if (kpIdFilter.HasValue)
                    {
                        args.Add("field", "kinopoisk_id");
                        args.Add("query", kpIdFilter.Value.ToString());
                    }
                    else if (imdbIdFilter != null)
                    {
                        args.Add("field", "imdb_id");
                        args.Add("query", imdbIdFilter);
                    }
                    else
                    {
                        args.Add("field", "title");
                        args.Add("query", searchRequest);
                        if (yearFilter.HasValue)
                        {
                            args.Add("year", yearFilter.Value.ToString());
                        }
                    }
                }

                var apiEndpoint = shortSearch
                    ? "/api/short"
                    : $"/api/{kindName}";

                var result = await siteProvider.HttpClient
                    .GetBuilder(new Uri(domain, apiEndpoint))
                    .WithArguments(args)
                    .SendAsync(cancellationToken)
                    .AsNewtonsoftJson<JObject>()
                    .ConfigureAwait(false);
                var results = result?["data"] as JArray;
                if (result == null || results == null)
                {
                    yield break;
                }

                maxPage = result["last_page"]?.ToIntOrNull() ?? -1;

                var items = results
                    .OfType<JObject>()
                    .Select(item =>
                    {
                        var itemInfo = VideoCDNItemInfoProvider.GetItemFromJObject(siteProvider, domain, item);
                        if (itemInfo == null)
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
                        else if (imdbIdFilter != null
                            && itemInfo.Details.LinkedIds.TryGetValue(Sites.IMDb, out var imdbId))
                        {
                            if (imdbIdFilter != imdbId)
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
                    .Where(t => t.item?.SiteId != null && t.item.Link != null)
                    .OrderBy(t => t.prox)
                    .GroupBy(t => t.item.SiteId, t => t.item)
                    .Select(group => group
                        .OrderBy(item => string.IsNullOrEmpty(item.Title))
                        .ThenBy(item => !item.Details.LinkedIds.ContainsKey(Sites.Kinopoisk))
                        .ThenBy(item => !item.Details.LinkedIds.ContainsKey(Sites.IMDb))
                        .First());

                foreach (var item in items)
                {
                    yield return item;
                }
            }
        }
    }
}

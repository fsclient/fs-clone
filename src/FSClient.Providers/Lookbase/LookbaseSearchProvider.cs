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

    public class LookbaseSearchProvider : ISearchProvider
    {
        private readonly LookbaseSiteProvider siteProvider;
        private readonly TMDbSearchProvider? tmdbSearchProvider;

        public LookbaseSearchProvider(
            LookbaseSiteProvider siteProvider,
            TMDbSearchProvider? tmdbSearchProvider)
        {
            this.siteProvider = siteProvider;
            this.tmdbSearchProvider = tmdbSearchProvider;
        }

        public IReadOnlyList<Section> Sections => Array.Empty<Section>();

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site && original.Link != null)
            {
                return new[] { original };
            }

            if (original.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpIdStr)
                && int.TryParse(kpIdStr, out var kpId))
            {
                return await GetFullResultInternal(kpIdFilter: kpId, cancellationToken: cancellationToken)
                    .Take(IncrementalLoadingCollection.DefaultCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            if (original.Details.LinkedIds.TryGetValue(Sites.IMDb, out var imdbIdStr))
            {
                return await GetFullResultInternal(imdbIdFilter: imdbIdStr, cancellationToken: cancellationToken)
                    .Take(IncrementalLoadingCollection.DefaultCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (tmdbSearchProvider != null)
            {
                var tmdbSimilar = await tmdbSearchProvider.FindSingleSimilarWithLinkedIdsAsync(original, cancellationToken).ConfigureAwait(false);
                if (tmdbSimilar != null && !cancellationToken.IsCancellationRequested)
                {
                    return await FindSimilarAsync(tmdbSimilar, cancellationToken).ConfigureAwait(false);
                }
            }

            return Enumerable.Empty<ItemInfo>();
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return AsyncEnumerable.Empty<ItemInfo>();
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return default;
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return AsyncEnumerable.Empty<ItemInfo>();
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            int? kpIdFilter = null, string? imdbIdFilter = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var apiLink = siteProvider.Properties[LookbaseSiteProvider.LookbaseApiDomainKey].ToUriOrNull(domain);

            var result = await siteProvider.HttpClient
                .GetBuilder(new Uri(apiLink, "/api/"))
                .WithArgument("id_kp", kpIdFilter?.ToString())
                .WithArgument("id_imdb", imdbIdFilter?.ToString())
                .WithArgument("token", Secrets.LookbaseApiKey)
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            var results = result?["data"] as JObject;
            if (result == null || results == null)
            {
                yield break;
            }

            var items = new[] { results }
                .OfType<JObject>()
                .Select(item =>
                {
                    var itemInfo = GetItemFromJObject(domain, item);
                    if (itemInfo == null)
                    {
                        return null;
                    }

                    if (kpIdFilter.HasValue
                        && itemInfo.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kinopoiskId))
                    {
                        if (kpIdFilter != int.Parse(kinopoiskId))
                        {
                            return null;
                        }
                    }
                    else if (imdbIdFilter != null
                        && itemInfo.Details.LinkedIds.TryGetValue(Sites.IMDb, out var imdbId))
                    {
                        if (imdbIdFilter != imdbId)
                        {
                            return null;
                        }
                    }

                    return itemInfo;
                })
                .Where(t => t?.SiteId != null && t.Link != null);

            foreach (var item in items)
            {
                yield return item!;
            }
        }

        private ItemInfo? GetItemFromJObject(Uri domain, JObject jObject)
        {
            if (jObject["id_local"]?.ToString() is not string id)
            {
                return null;
            }

            var item = new LookbaseItemInfo(siteProvider.Site, id)
            {
                Title = (jObject["ua_name"] ?? jObject["orig_name"])?.ToString(),
                Link = jObject["link"]?.ToUriOrNull(domain),
                Translation = (jObject["sounds"] as JArray)?.FirstOrDefault()?.ToString(),
                Details =
                {
                    TitleOrigin = jObject["orig_name"]?.ToString(),
                }
            };
            if (item.Link == null)
            {
                return null;
            }

            if (jObject["id_kp"]?.ToString() is string kpId)
            {
                item.Details.LinkedIds.Add(Sites.Kinopoisk, kpId.ToString());
            }
            if (jObject["id_imdb"]?.ToString() is string imdbId)
            {
                item.Details.LinkedIds.Add(Sites.IMDb, imdbId);
            }

            return item;
        }
    }
}

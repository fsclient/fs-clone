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

    using Nito.AsyncEx;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public class YohohoSearchProvider : ISearchProvider
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly YohohoSiteProvider siteProvider;
        private readonly SemaphoreSlim semaphoreSlim;
        private (ItemInfo item, bool ignoreLinkedId, IDictionary<string, YohohoSearchResult> result) tempCache;

        public YohohoSearchProvider(YohohoSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            semaphoreSlim = new SemaphoreSlim(1);
        }

        public IReadOnlyList<Section> Sections => Array.Empty<Section>();

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public Site Site => siteProvider.Site;

        public Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return Task.FromResult((IEnumerable<ItemInfo>)new[] { original });
            }

            var item = new ItemInfo(Site, $"yhh{original.SiteId}")
            {
                Title = original.Title,
                Section = original.Section,
                Details =
                {
                    TitleOrigin = original.Details.TitleOrigin,
                    Year = original.Details.Year
                }
            };

            if (original.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpId))
            {
                item.Details.LinkedIds.Add(Sites.Kinopoisk, kpId);
            }
            if (original.Details.LinkedIds.TryGetValue(Sites.IMDb, out var imdb))
            {
                item.Details.LinkedIds.Add(Sites.IMDb, imdb);
            }

            return Task.FromResult(new[] { item }.AsEnumerable());
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return AsyncEnumerable.Empty<ItemInfo>();
        }

        internal async Task<IDictionary<string, YohohoSearchResult>> GetRelatedResultsAsync(
            ItemInfo itemInfo, bool ignoreLinkedId, CancellationToken cancellationToken)
        {
            var cache = tempCache;
            if (cache.item == itemInfo && cache.ignoreLinkedId == ignoreLinkedId)
            {
                return cache.result;
            }

            using var _ = await semaphoreSlim.LockAsync(cancellationToken).ConfigureAwait(false);

            cache = tempCache;
            if (cache.item == itemInfo && cache.ignoreLinkedId == ignoreLinkedId)
            {
                return cache.result;
            }

            string? title = null;
            int? kpId = null;

            if (!ignoreLinkedId
                && itemInfo.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpIdStr))
            {
                kpId = kpIdStr.ToIntOrNull();
            }
            if (!kpId.HasValue)
            {
                title = itemInfo.GetTitles(true).FirstOrDefault();

                if (title == null)
                {
                    return new Dictionary<string, YohohoSearchResult>();
                }
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var referer = new Uri(siteProvider.Properties[YohohoSiteProvider.YohohoRefererKey], UriKind.Absolute);

            var body = new Dictionary<string, string>
            {
                ["tv"] = "1",
                ["resize"] = "1",
                ["player"] = "hdvb,bazon,ustore,videocdn,kodik,trailer,torrent"
                //["player"] = "collaps,hdvb,bazon,ustore,alloha,videocdn,iframe,kodik,pleer,trailer,torrent",
            };

            var builder = siteProvider.HttpClient
                .PostBuilder(domain);

            if (title != null)
            {
                body.Add("title", title);
            }
            if (kpId.HasValue)
            {
                body.Add("kinopoisk", kpId.ToString());
            }

            var response = await builder
                .WithHeader("Origin", referer.GetOrigin())
                .WithHeader("Referer", referer.ToString())
                .WithBody(body)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsJson<IDictionary<string, YohohoSearchResult?>>(cancellationToken)
                .ConfigureAwait(false);

            var result = response?
                .Where(p => p.Value?.IFrame != null)
                .ToDictionary(p => p.Key, p => p.Value!)
                ?? new Dictionary<string, YohohoSearchResult>();

            tempCache = (itemInfo, ignoreLinkedId, result);

            return result;
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return default;
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return AsyncEnumerable.Empty<ItemInfo>();
        }
    }
}

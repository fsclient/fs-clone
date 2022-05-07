namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class FindAnimeSearchProvider : ISearchProvider
    {
        private readonly FindAnimeSiteProvider siteProvider;

        public FindAnimeSearchProvider(FindAnimeSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.Any
        };

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return new[] { original };
            }

            if (!original.Section.Modifier.HasFlag(SectionModifiers.Cartoon))
            {
                return Enumerable.Empty<ItemInfo>();
            }

            var titles = original.GetTitles();
            return await original
                .GetTitles()
                .Select(t => new Func<CancellationToken, Task<List<ItemInfo>>>(ct => GetShortResultInternal(t, ct)
                    .Select(item => new
                    {
                        Prox = Math.Max(
                            titles.Select(t => item.Title?.Proximity(t, false) ?? 0).Max(),
                            titles.Select(t => item.Details.TitleOrigin?.Proximity(t, false) ?? 0).Max()),
                        Value = item
                    })
                    .Where(obj => obj.Prox > 0.9)
                    .OrderByDescending(obj => obj.Prox)
                    .Select(obj => obj.Value)
                    .ToListAsync(ct)
                    .AsTask()))
                .WhenAny(item => item?.Any() == true, new List<ItemInfo>(), token: cancellationToken)
                .ConfigureAwait(false);
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetShortResultInternal(filter.SearchRequest);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetShortResultInternal(request);
        }

        public async IAsyncEnumerable<ItemInfo> GetShortResultInternal(
            string request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var json = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, "/search/suggestion/"))
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .WithAjax()
                .WithArgument("query", request)
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            if (json?["suggestions"] is not JArray suggestions)
            {
                yield break;
            }

            var items = suggestions
                .OfType<JObject>()
                .Select(item =>
                {
                    var id = item["link"]?.ToString().Trim('/');
                    var isAdditional = item["additional"] is { } additionalJson && additionalJson.Type != JTokenType.Null;
                    if (string.IsNullOrEmpty(id) || isAdditional)
                    {
                        return null;
                    }
                    var link = item["link"]?.ToUriOrNull(domain);
                    var title = item["value"]?.ToString();
                    var thumbnail = item["thumbnail"]?.ToUriOrNull(domain);
                    var titleOrigin = string.Join(" / ", item["names"]?.ToArray()?.Select(t => t.ToString()) ?? Enumerable.Empty<string>());
                    return new ItemInfo(Site, id)
                    {
                        Link = link,
                        Title = title,
                        Section = Section.CreateDefault(SectionModifiers.Anime),
                        Poster = new WebImage
                        {
                            [ImageSize.Thumb] = thumbnail
                        },
                        Details =
                        {
                            TitleOrigin = titleOrigin
                        }
                    };
                })
                .Where(item => !string.IsNullOrEmpty(item?.SiteId) && item?.Link != null)!;

            foreach (var item in items)
            {
                yield return item!;
            }
        }
    }
}

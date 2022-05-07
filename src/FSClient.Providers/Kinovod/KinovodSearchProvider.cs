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
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class KinovodSearchProvider : ISearchProvider
    {
        private readonly KinovodSiteProvider siteProvider;

        public KinovodSearchProvider(KinovodSiteProvider KinovodSiteProvider)
        {
            siteProvider = KinovodSiteProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.Any
        };

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return Task.FromResult((IEnumerable<ItemInfo>)new[] { original });
            }

            var allTitles = original.GetTitles().Select(t => t.GetLettersAndDigits()).ToArray();
            var section = original.Section.Modifier.HasFlag(SectionModifiers.TVShow) ? Section.CreateDefault(SectionModifiers.TVShow)
                : original.Section.Modifier.HasFlag(SectionModifiers.Serial) ? Section.CreateDefault(SectionModifiers.Serial)
                : original.Section.Modifier.HasFlag(SectionModifiers.Film) ? Section.CreateDefault(SectionModifiers.Film)
                : Section.Any;

            return original
                .GetTitles(true)
                .Select(t => new Func<CancellationToken, Task<IEnumerable<ItemInfo>>>(ct =>
                    GetShortResultInternal(t, section, original.Details.Year, ct)
                        .Select(item => (
                            prox: allTitles.Max(t => t.Proximity(item.Title ?? string.Empty)),
                            item: item
                        ))
                        .Where(tuple => tuple.prox > 0.8)
                        .OrderByDescending(tuple => tuple.prox)
                        .Select(tuple => tuple.item)
                        .Take(IncrementalLoadingCollection.DefaultCount)
                        .ToEnumerableAsync(ct)))
                .WhenAny(item => item?.Any() == true, Enumerable.Empty<ItemInfo>(), token: cancellationToken);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section, DisplayItemMode.Minimal));
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetShortResultInternal(filter.SearchRequest, filter.PageParams.Section, null);
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetShortResultInternal(request, section, null);
        }

        private async IAsyncEnumerable<ItemInfo> GetShortResultInternal(
            string request, Section section, int? year,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var json = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, $"/ajax/search.php"))
                .WithArgument("query", request)
                .WithHeader("Referer", domain.ToString())
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            if (json?["suggestions"] is not JArray items)
            {
                yield break;
            }

            var results = items
                .OfType<JObject>()
                .Select(jObject => (
                    url: jObject["url"]?.ToUriOrNull(),
                    entries: jObject["url"]?.ToString().Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries),
                    value: jObject["value"]?.ToString(),
                    year: jObject["year"]?.ToIntOrNull()
                ))
                .Where(item => item.url != null)
                .Select(item => (
                    section: item.entries.FirstOrDefault() switch
                    {
                        "serial" => Section.CreateDefault(SectionModifiers.Serial),
                        "film" => Section.CreateDefault(SectionModifiers.Film),
                        "tv_show" => Section.CreateDefault(SectionModifiers.TVShow),
                        _ => Section.Any
                    },
                    id: item.entries.LastOrDefault()?.Split('-').FirstOrDefault().ToIntOrNull(),
                    url: item.url,
                    value: item.value,
                    year: item.year
                ))
                .Where(item => item.id.HasValue && item.section.Modifier.HasFlag(section.Modifier) && (!year.HasValue || year == item.year))
                .Select(item => new ItemInfo(Site, item.id.ToString())
                {
                    Title = item.value,
                    Link = item.url,
                    Section = item.section,
                    Details =
                    {
                        Year = item.year
                    }
                });

            foreach (var item in results)
            {
                yield return item;
            }
        }
    }
}

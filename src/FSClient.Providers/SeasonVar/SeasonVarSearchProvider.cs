namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class SeasonVarSearchProvider : ISearchProvider
    {
        private static readonly Regex yearFinder = new Regex(@"\((?<year>\d{4})\)");
        private readonly SeasonVarSiteProvider siteProvider;

        public SeasonVarSearchProvider(SeasonVarSiteProvider seasonVarSiteProvider)
        {
            siteProvider = seasonVarSiteProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.CreateDefault(SectionModifiers.Serial)
        };

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            ItemInfo? similarItem;

            if (original.Site == Site)
            {
                similarItem = await siteProvider.EnsureItemAsync(original, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (original.Section.Modifier.HasFlag(SectionModifiers.Film))
                {
                    return Enumerable.Empty<ItemInfo>();
                }

                similarItem = await original.Details.Titles
                   .Select(title => new Func<CancellationToken, Task<ItemInfo?>>(async ct =>
                   {
                       if (title == null)
                       {
                           return null;
                       }

                       var request = string.IsNullOrWhiteSpace(original.Details.TitleOrigin)
                           ? title
                           : $"{title} / {original.Details.TitleOrigin}";

                       return await GetShortResult(request, Section.Any)
                           .Select(item => new
                           {
                               Prox = Math.Max(
                                   item.Title?.Proximity(title, false) ?? 0,
                                   item.Details.TitleOrigin?.Proximity(original.Details.TitleOrigin ?? "", false) ?? 0),
                               Value = item
                           })
                           .Where(obj => obj.Prox > 0.9)
                           .OrderByDescending(obj => obj.Prox)
                           .Select(obj => obj.Value)
                           .FirstOrDefaultAsync(ct)
                           .ConfigureAwait(false);
                   }))
                   .WhenAny(res => res != null, null, cancellationToken)
                   .ConfigureAwait(false);
            }

            if (similarItem == null)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            var anotherSeasonItems = await GetAnotherAsync(similarItem, cancellationToken).ConfigureAwait(false);

            return anotherSeasonItems
                .Union(new[] { similarItem })
                .Where(i => !string.IsNullOrWhiteSpace(i?.SiteId) && i!.Link != null)
                .OrderBy(i => i!.Details.Status.CurrentSeason);
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SearchPageParams?>(new SearchPageParams(Site, section, DisplayItemMode.Minimal));
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return GetShortResultInternal(request);
        }

        private async IAsyncEnumerable<ItemInfo> GetShortResultInternal(
            string request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var json = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, "autocomplete.php"))
                .WithArgument("query", request)
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            foreach (var item in ParseItemsFromShortSearchJson(json, domain))
            {
                yield return item;
            }
        }

        private async Task<IEnumerable<ItemInfo>> GetAnotherAsync(ItemInfo item, CancellationToken cancellationToken)
        {
            if (item.Link == null)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            if (!item.Link.IsAbsoluteUri)
            {
                var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
                item.Link = new Uri(domain, item.Link);
            }

            return item.Link.Host.StartsWith("1seasonvar", StringComparison.Ordinal)
                ? await GetAnotherFromApiAsync(item, cancellationToken).ConfigureAwait(false)
                : await GetAnotherFromFullAsync(item, cancellationToken).ConfigureAwait(false);
        }

        private IEnumerable<ItemInfo> ParseItemsFromShortSearchJson(JObject? json, Uri domain)
        {
            if (json?["suggestions"] is not JObject suggestions)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            return InternalEnumerable(domain, json, suggestions)
                // group by same kp votes count and value and take one from group
                .Select(tuple =>
                {
                    // Игра престолов (2011) / Game of Thrones (1 сезон)
                    // Игра престолов / Game of Thrones (1 сезон)
                    // Игра престолов (2011) / Game of Thrones
                    // Игра престолов / Game of Thrones
                    // Игра престолов (1 сезон)
                    // Игра престолов (2011) 

                    var regex = new Regex(
                        @"(?<title>[^\/(]+)(?:(?:\((?<year>\d{4})\))|(?:\((?<seas>\d{1,2})\s+[^\)]*\)))?",
                        RegexOptions.Compiled);
                    var parts = tuple.title.Split(new[] { " / " }, StringSplitOptions.None);
                    var ruRegexed = regex.Match(parts.FirstOrDefault() ?? "");
                    var enRegexed = regex.Match(parts.Skip(1).FirstOrDefault() ?? "");

                    return new ItemInfo(Site, tuple.id.ToString())
                    {
                        Title = ruRegexed.Groups["title"].Value?.Trim(),
                        Link = tuple.link,
                        Section = Section.CreateDefault(SectionModifiers.Serial),

                        Details =
                        {
                            Status = new Status(
                                enRegexed.Groups["seas"].Value?.ToIntOrNull() ?? ruRegexed.Groups["seas"].Value?.ToIntOrNull() ?? 1
                            ),
                            TitleOrigin = enRegexed.Groups["title"].Value?.Trim(),
                            Year = ruRegexed.Groups["year"].Value?.ToIntOrNull() ?? enRegexed.Groups["year"].Value?.ToIntOrNull()
                        }
                    };
                })
                .GroupBy(item => (item.Title, item.Details.TitleOrigin))
                .Select(g => g
                .OrderByDescending(item => item.Details.Status.CurrentSeason ?? 0)
                .ThenByDescending(item => item.Details.Year ?? 0)
                .First());

            // J4L
            IEnumerable<(string title, int? id, Uri link)> InternalEnumerable(Uri inDomain, JObject inJson, JObject inSuggestions)
            {
                var titles = (inSuggestions["valu"] as JArray)?.Select(title => title.ToString()).GetEnumerator();
                var ids = (inJson["id"] as JArray)?.Select(id => id.ToIntOrNull()).GetEnumerator();
                var links = (inJson["data"] as JArray)?.Select(link => link?.ToString()?.TrimStart('/')
                    .ToUriOrNull(inDomain)).GetEnumerator();

                if (titles is null
                    || ids is null
                    || links is null)
                {
                    yield break;
                }

                try
                {
                    while (titles.MoveNext() && ids.MoveNext() && links.MoveNext())
                    {
                        if (!string.IsNullOrEmpty(titles.Current)
                            && ids.Current.HasValue
                            && links.Current?.AbsolutePath.Contains("serial") == true)
                        {
                            yield return (titles.Current, ids.Current, links.Current);
                        }
                    }
                }
                finally
                {
                    titles.Dispose();
                    ids.Dispose();
                    links.Dispose();
                }
            }
        }

        private async Task<IEnumerable<ItemInfo>> GetAnotherFromFullAsync(ItemInfo item, CancellationToken cancellationToken)
        {
            if (item.Link is not Uri url)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            var html = await siteProvider
                .HttpClient
                .GetBuilder(url)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            var seasons = html?.QuerySelectorAll(".pgs-seaslist a");
            if (seasons == null)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            return seasons
                .Select(season =>
                {
                    var seasonLink = season.GetAttribute("href")?.TrimStart('/');
                    if (!Uri.TryCreate(item.Link, seasonLink, out var seasonUri))
                    {
                        return null;
                    }

                    var id = seasonUri.Segments.LastOrDefault()?.Split('-')[1];
                    if (id == null)
                    {
                        return null;
                    }

                    var contentText = season.FirstChild?.TextContent ?? season.TextContent;
                    var seasonTitle = contentText == null
                        ? null
                        : Regex.Replace(contentText, @"((?:\([^\)]+\))|>+|\s{2,})", " ").Trim();

                    int? seasonNumber = null;
                    var titleParts = seasonTitle?.ToLower().Split(new[] { ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                    var seasonIndex = Array.LastIndexOf(titleParts, "сезон") - 1;
                    if (seasonIndex > 0
                        && int.TryParse(titleParts?[seasonIndex], out var temp))
                    {
                        seasonNumber = temp;
                    }

                    return new ItemInfo(Site, id)
                    {
                        Title = seasonTitle,
                        Link = seasonUri,
                        Details =
                        {
                            Status = new Status(seasonNumber)
                        }
                    };
                })
                .Where(i => i != null)!;
        }

        private async Task<IEnumerable<ItemInfo>> GetAnotherFromApiAsync(ItemInfo item, CancellationToken cancellationToken)
        {
            if (item.Link is not Uri url)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            var seasons = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(item.Link, "api/all_seasons"))
                .WithArgument("url", url.ToString())
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JArray>()
                .ConfigureAwait(false);
            if (seasons == null)
            {
                return Enumerable.Empty<ItemInfo>();
            }

            return seasons
                .Select(season =>
                {
                    var seasonLink = season["link"]?.ToString()?.TrimStart('/').ToUriOrNull(item.Link);
                    if (seasonLink == null)
                    {
                        return null;
                    }

                    var id = seasonLink.Segments.LastOrDefault()?.Split('-')[1];
                    if (id == null)
                    {
                        return null;
                    }

                    var contentText = season["title"]?.ToString();
                    var seasonTitle = contentText == null
                        ? null
                        : Regex.Replace(contentText, @"((?:\([^\)]+\))|>+|\s{2,})", " ").Trim();

                    int? seasonNumber = null;
                    var titleParts = seasonTitle?.ToLower().Split(new[] { ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                    var seasonIndex = Array.LastIndexOf(titleParts, "сезон") - 1;
                    if (seasonIndex > 0
                        && int.TryParse(titleParts?[seasonIndex], out var temp))
                    {
                        seasonNumber = temp;
                    }

                    return new ItemInfo(Site, id)
                    {
                        Title = seasonTitle,
                        Link = seasonLink,
                        Details =
                        {
                            Status = new Status(seasonNumber)
                        }
                    };
                })
                .Where(i => i != null)!;
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return GetFullResultInternal(filter);
        }

        private async IAsyncEnumerable<ItemInfo> GetFullResultInternal(SearchPageFilter filter,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int page = 0, maxPage = 1;
            var searchRequest = filter.SearchRequest;

            while (page < maxPage)
            {
                var currentPage = ++page;

                var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
                var requestLink = domain;
                if (domain.Host.Contains("seasonhit-api"))
                {
                    if (currentPage > 1)
                    {
                        yield break;
                    }

                    requestLink = new Uri(domain, "autocomplete.php");
                }

                var responseText = await siteProvider
                    .HttpClient
                    .GetBuilder(requestLink)
                    .WithArgument("mode", "search")
                    .WithArgument("page", currentPage.ToString())
                    .WithArgument("query", searchRequest)
                    .WithHeader("Referer", domain.ToString())
                    .WithAjax()
                    .SendAsync(cancellationToken)
                    .AsText()
                    .ConfigureAwait(false);
                if (responseText == null)
                {
                    yield break;
                }

                if (JsonHelper.ParseOrNull<JObject>(responseText) is JObject shortSearchJson)
                {
                    foreach (var item in ParseItemsFromShortSearchJson(shortSearchJson, domain))
                    {
                        yield return item;
                    }
                    yield break;
                }

                var html = WebHelper.ParseHtml(responseText);
                if (html == null)
                {
                    yield break;
                }

                maxPage = html.QuerySelectorAll(".pgs-search-page a[href]")
                    .Reverse()
                    .Select(li => li.TextContent.ToIntOrNull())
                    .FirstOrDefault(p => p != null) ?? -1;

                var items = html
                    .QuerySelectorAll(".pgs-search-wrap")
                    .Select(div =>
                    {
                        var link = div.QuerySelector("a[href$=html]")?.GetAttribute("href")?.TrimStart('/').ToUriOrNull(domain);
                        var id = link?.LocalPath.Split('-').Skip(1).FirstOrDefault()?.ToIntOrNull();
                        if (!id.HasValue)
                        {
                            return null;
                        }

                        var poster = div.QuerySelector(".pst img[src]")?.GetAttribute("src")?.Replace("/small/", "/")
                            .ToUriOrNull(domain);
                        var titles = div.QuerySelectorAll(".pgs-search-info a").Select(a => a.TextContent)
                            .Where(a => !string.IsNullOrEmpty(a));
                        var ruTitle = titles.FirstOrDefault();
                        var enTitle = titles.Skip(1).FirstOrDefault();
                        var desc = div.QuerySelector(".pgs-search-info p")?.TextContent;

                        var year = yearFinder.Match(ruTitle ?? "").Groups["year"].Value.ToIntOrNull();
                        if (year.HasValue)
                        {
                            ruTitle = yearFinder.Replace(ruTitle, "").Trim();
                        }

                        return new ItemInfo(siteProvider.Site, id.ToString())
                        {
                            Link = link,
                            Poster = poster,
                            Title = ruTitle,
                            Section = Section.CreateDefault(SectionModifiers.Serial),
                            Details =
                            {
                                    TitleOrigin = enTitle,
                                    Year = year,
                                    Description = desc
                            }
                        };
                    })
                    .Where(i => i != null);

                foreach (var item in items)
                {
                    yield return item!;
                }
            }
        }
    }
}

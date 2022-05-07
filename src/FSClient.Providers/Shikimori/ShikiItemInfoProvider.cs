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

    public class ShikiItemInfoProvider : IItemInfoProvider
    {
        private readonly ShikiSiteProvider siteProvider;

        public ShikiItemInfoProvider(ShikiSiteProvider shikiSiteProvider)
        {
            siteProvider = shikiSiteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return link != null && link.Host.Contains("shikimori");
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            var id = ShikiSiteProvider.GetIdFromLink(link);

            if (!id.HasValue)
            {
                return null;
            }

            var item = new ItemInfo(Site, id.ToString());
            return await PreloadItemAsync(item, PreloadItemStrategy.Poster, cancellationToken).ConfigureAwait(false)
                ? item
                : null;
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            if (item == null)
            {
                return Task.FromResult(false);
            }

            if (preloadItemStrategy == PreloadItemStrategy.Poster
                && item.Details.Rating != null)
            {
                return Task.FromResult(true);
            }

            var tasks = new[] { PreloadItemInfoFromAnimesApiAsync(item, cancellationToken) }.AsEnumerable();

            if (preloadItemStrategy == PreloadItemStrategy.Full)
            {
                tasks = tasks.Concat(new[]
                {
                    PreloadSimilarItemsFromApiAsync(item, cancellationToken),
                    PreloadFranchiseItemsFromApiAsync(item, cancellationToken),
                    PreloadLinkedLinksFromApiAsync(item, cancellationToken)
                });
            }

            return tasks.ToAsyncEnumerable()
                .WhenAll((t, _) => t)
                .AllAsync(r => r, cancellationToken)
                .AsTask();
        }

        private async Task<bool> PreloadItemInfoFromAnimesApiAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var json = await siteProvider
                .GetFromApiAsync($"/api/animes/{itemInfo.SiteId}", cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            if (json == null)
            {
                return false;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var result = UpdateItemInfoFromAnimesApiJson(domain, itemInfo, json);
            if (result
                && itemInfo.Section.Modifier.HasFlag(SectionModifiers.Serial))
            {
                var (nextEpisode, hasEpisodes) = GetNextEpisode(itemInfo, json["next_episode_at"]?.ToString());
                if (hasEpisodes)
                {
                    itemInfo.Details.EpisodesCalendar = GetEpisodesCalendar(itemInfo, nextEpisode, default);
                }
            }

            return result;
        }

        private async Task<bool> PreloadSimilarItemsFromApiAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (itemInfo.Details.Similar.Any())
            {
                return true;
            }

            var json = await siteProvider
                .GetFromApiAsync($"/api/animes/{itemInfo.SiteId}/similar", cancellationToken)
                .AsNewtonsoftJson<JArray>()
                .ConfigureAwait(false);
            if (json == null)
            {
                return false;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            itemInfo.Details.Similar = json
                .OfType<JObject>()
                .Where(jsonItem => jsonItem["id"]?.ToIntOrNull() != null)
                .Select(jsonItem =>
                {
                    var similarItem = new ItemInfo(Site, jsonItem["id"]?.ToString());
                    if (!UpdateItemInfoFromAnimesApiJson(domain, similarItem, jsonItem))
                    {
                        return null;
                    }

                    return similarItem;
                })
                .Where(similarItem => similarItem != null)
                .ToArray()!;

            return true;
        }


        private async Task<bool> PreloadFranchiseItemsFromApiAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (itemInfo.Details.Franchise.Any())
            {
                return true;
            }

            var json = await siteProvider
                .GetFromApiAsync($"/api/animes/{itemInfo.SiteId}/franchise", cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            if (json == null)
            {
                return false;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var jsonNodes = json["nodes"] as JArray;
            var today = DateTime.Today;

            if (jsonNodes?.Type == JTokenType.Array
                && jsonNodes.HasValues)
            {
                itemInfo.Details.Franchise = jsonNodes
                    .OfType<JObject>()
                    .Select((jsonItem) =>
                    {
                        var date = jsonItem["date"]?.ToObject<long>();
                        if (DateTimeOffset.FromUnixTimeSeconds(date ?? 0) > today)
                        {
                            return default;
                        }

                        var id = jsonItem["id"]?.ToIntOrNull() ?? 0;
                        if (id.ToString() == itemInfo.SiteId)
                        {
                            return (date, id, item: itemInfo);
                        }

                        var item = new ItemInfo(siteProvider.Site, jsonItem["id"]?.ToString());
                        var result = UpdateItemInfoFromAnimesApiJson(domain, item, jsonItem);
                        return result ? (date, id, item) : default;
                    })
                    .Where(tuple => tuple.id > 0)
                    .OrderBy(tuple => tuple.date ?? int.MaxValue)
                    .Select(tuple => tuple.item)
                    .ToList();
                return true;
            }
            // We can ignore empty Franchise
            return true;
        }

        private async Task<bool> PreloadLinkedLinksFromApiAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (itemInfo.Details.LinkedIds.Any())
            {
                return true;
            }

            var json = await siteProvider
                .GetFromApiAsync($"/api/animes/{itemInfo.SiteId}/external_links", cancellationToken)
                .AsNewtonsoftJson<JArray>()
                .ConfigureAwait(false);
            if (json == null)
            {
                return false;
            }

            foreach (var siteJson in json)
            {
                var link = siteJson["url"]?.ToUriOrNull();
                if (link == null)
                {
                    continue;
                }

                var (site, id) = siteJson["kind"]?.ToString() switch
                {
                    "world_art" => (Sites.WorldArt, QueryStringHelper.ParseQuery(link.Query).FirstOrDefault(p => p.Key == "id").Value), // ?id=9308
                    "kinopoisk" => (Sites.Kinopoisk, link.Segments.Skip(2).FirstOrDefault()?.GetDigits()), // /series/1113120/
                    "twitter" => (Sites.Twitter, link.Segments.Skip(1).FirstOrDefault()), // /DARLI_FRA
                    "myanimelist" => (Sites.MyAnimeList, link.Segments.Skip(2).FirstOrDefault()?.GetDigits()), // /anime/35849
                    "imdb" => (Sites.IMDb, link.Segments.Skip(2).FirstOrDefault()?.Trim('/')), // /title/tt7865090/
                    _ => (Site.Any, (string?)null)
                };
                if (site == Site.Any || string.IsNullOrEmpty(id))
                {
                    continue;
                }

                itemInfo.Details.LinkedIds[site] = id!;
            }
            return true;
        }

        public static bool UpdateItemInfoFromAnimesApiJson(Uri domain, ItemInfo item, JObject json)
        {
            if (json == null
                || json["code"]?.ToIntOrNull() != null)
            {
                return false;
            }

            if (json["url"]?.ToUriOrNull(domain) is Uri url)
            {
                item.Link = url;
            }

            if (json["russian"]?.ToString() is string russianName
                && !string.IsNullOrWhiteSpace(russianName))
            {
                item.Details.TitleOrigin = json["name"]?.ToString();
                item.Title = russianName;
            }
            else
            {
                item.Title = json["name"]?.ToString();
            }

            item.Details.Year = int.TryParse(json["aired_on"]?.ToString()?.Split('-').First(), out var year) ? year : (int?)null;
            item.Poster = ShikiSiteProvider.GetImage(json["image"] ?? json["image_url"], domain);

            if (json["description_html"]?.ToString() is string desc)
            {
                var descNode = WebHelper.ParseHtml(desc);
                if (descNode != null)
                {
                    item.Details.Description = ShikiSiteProvider.ParseShikiHtml(descNode);
                }
            }

            if (json["kind"]?.ToString() is string kind)
            {
                item.Section = GetSectionFromShikiKind(kind, json["episodes"]?.ToIntOrNull() ?? 0);
            }

            if (json["score"]?.ToDoubleOrNull() is double score)
            {
                item.Details.Rating = new NumberBasedRating(10, score);
            }

            item.Details.Status = GetStatusFromApiJson(json);

            item.Details.Tags = item.Details.Tags
                .Concat(GetTagsFromAnimeApiJson(json))
                .GroupBy(t => t.Title)
                .Select(g => g.Last())
                .ToArray();

            item.Details.Images = (json["screenshots"] as JArray)?
                .Select(screenshot => ShikiSiteProvider.GetImage(screenshot as JObject, domain))
                .Where(image => image.Count > 0)
                .ToArray()
                ?? Array.Empty<WebImage>();

            return true;
        }

        public static Section GetSectionFromShikiKind(string kind, int episodes = 0)
        {
            var section = ShikiSiteProvider.Sections.FirstOrDefault(s =>
                kind.StartsWith(s.Value, StringComparison.OrdinalIgnoreCase)
                || kind.StartsWith(s.Title, StringComparison.OrdinalIgnoreCase));
            if (episodes > 1 && section.Modifier.HasFlag(SectionModifiers.Film))
            {
                section = ShikiSiteProvider.Sections.FirstOrDefault(s => s.Modifier.HasFlag(SectionModifiers.Serial));
            }
            return section;
        }

        private static (EpisodeInfo? nextEpisode, bool hasEpisodes) GetNextEpisode(ItemInfo item, string? nextEpisodeAt)
        {
            if (item.Link == null)
            {
                return (null, false);
            }

            if (DateTimeOffset.TryParse(nextEpisodeAt, out var date))
            {
                var nextEpisode = new EpisodeInfo
                {
                    Season = -1,
                    Episode = (item.Details.Status.CurrentEpisode ?? item.Details.Status.TotalEpisodes ?? 0) + 1,
                    DateTime = date
                };
                return (nextEpisode, true);
            }
            else if (item.Details.Year == null
                || item.Details.Year < 2016)
            {
                return (null, false);
            }

            return (null, true);
        }

        private async IAsyncEnumerable<EpisodeInfo>? GetEpisodesCalendar(
            ItemInfo item, EpisodeInfo? nextEpisode,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (nextEpisode != null)
            {
                yield return nextEpisode;
            }

            var html = await siteProvider.HttpClient
                .GetBuilder(item.Link!)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (html == null)
            {
                yield break;
            }

            var episodes = html
                .QuerySelectorAll(".history .entry:not([itemprop=discussionUrl])")
                .Select(entry => (
                    episode: entry.QuerySelector(".name")?.TextContent?.Split(' ').Skip(1).FirstOrDefault()?.ToIntOrNull(),
                    hasDate: DateTimeOffset.TryParse(entry.QuerySelector("time[datetime]")?.GetAttribute("datetime"), out var dateTime),
                    dateTime
                ))
                .Where(ep => ep.episode.HasValue && ep.hasDate)
                .Select(ep => new EpisodeInfo
                {
                    Season = -1,
                    Episode = ep.episode,
                    DateTime = ep.dateTime
                });
            foreach (var episode in episodes)
            {
                yield return episode;
            }
        }

        private static Status GetStatusFromApiJson(JObject json)
        {
            var jsonStatus = json["status"]?.ToString();
            var type = jsonStatus == "anons" ? StatusType.Anons
                : jsonStatus == "ongoing" ? StatusType.Ongoing
                : jsonStatus == "released" ? StatusType.Released
                : StatusType.Unknown;
            return new Status(
                currentEpisode: json["episodes_aired"]?.ToIntOrNull() is int current && current > 0 ? current : (int?)null,
                totalEpisodes: json["episodes"]?.ToIntOrNull() is int total && total > 0 ? total : (int?)null,
                type: type
            );
        }

        private static IEnumerable<TagsContainer> GetTagsFromAnimeApiJson(JObject json)
        {
            if (json["rating"]?.ToString() is string rating
                && rating != "none")
            {
                var tag = new TitledTag(rating.Replace("_plus", "+").Replace('_', '-').ToUpper().Replace('X', 'x'), ShikiSiteProvider.SiteKey, "rating", rating);
                yield return new TagsContainer("Рейтинг", tag);
            }

            var studios = (json["studios"] as JArray)?
                .Select(s => (
                    title: (s["russian"] ?? s["name"])?.ToString(),
                    value: s["id"]?.ToString()
                ))
                .Select(t => new TitledTag(t.title, ShikiSiteProvider.SiteKey, "studio", t.value!))
                .ToArray()
                ?? Array.Empty<TitledTag>();
            if (studios.Length > 0)
            {
                yield return new TagsContainer(TagType.Studio, studios);
            }

            var genres = (json["genres"] as JArray)?
                .Select(s => (
                    title: (s["russian"] ?? s["name"])?.ToString(),
                    value: s["id"]?.ToString()
                ))
                .Where(t => !string.IsNullOrWhiteSpace(t.title) && !string.IsNullOrWhiteSpace(t.value))
                .Select(t => new TitledTag(t.title, ShikiSiteProvider.SiteKey, "genre", t.value!))
                .ToArray()
                ?? Array.Empty<TitledTag>();
            if (genres.Length > 0)
            {
                yield return new TagsContainer(TagType.Genre, genres);
            }
        }
    }
}

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
    using FSClient.Shared.Services;

    using Newtonsoft.Json.Linq;

    public class TMDbItemInfoProvider : IItemInfoProvider
    {
        private readonly TMDbSiteProvider siteProvider;
        private readonly ICacheService cacheService;
        private readonly Regex linkRegex;

        public TMDbItemInfoProvider(
            TMDbSiteProvider siteProvider,
            ICacheService cacheService)
        {
            this.siteProvider = siteProvider;
            this.cacheService = cacheService;

            linkRegex = new Regex(@"(?:themoviedb\.org)?\/+(movie|tv)\/+(\d+)(?:-(.*?)(?:$|\/))?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private static readonly Dictionary<string, StatusType> availableStatuses = new Dictionary<string, StatusType>
        {
            ["Returning Series"] = StatusType.Ongoing,
            ["Ended"] = StatusType.Released,
            ["Released"] = StatusType.Released,
            ["Canceled"] = StatusType.Canceled,
            ["Pilot"] = StatusType.Pilot,
            ["In Production"] = StatusType.InProduction,
            ["Planned"] = StatusType.Anons,
            ["Post Production"] = StatusType.PostProduction,
            ["Rumored"] = StatusType.Rumored
        };

        public Site Site => siteProvider.Site;

        public bool CanPreload => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return link != null && siteProvider.Mirrors.Any(m => link.Host == m.Host) && linkRegex.IsMatch(link.ToString());
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            var match = linkRegex.Match(link.ToString());
            if (!match.Success)
            {
                return null;
            }

            var section = match.Groups[1].Value.ToLower();
            var id = match.Groups[2].Value;

            var json = await siteProvider
                .GetFromApiAsync(
                    $"{section}/{id}",
                    new Dictionary<string, string?>
                    {
                        ["append_to_response"] = "external_ids"
                    },
                    cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            if (json == null)
            {
                return null;
            }

            return await CreateItemInfoFromJsonAsync(json, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<ItemInfo?> CreateItemInfoFromJsonAsync(JObject jObject, CancellationToken cancellationToken)
        {
            if (jObject?["id"]?.ToString() is string id)
            {
                var itemInfo = new ItemInfo(TMDbSiteProvider.SiteKey, id);
                await FillItemInfoAsync(itemInfo, jObject, cancellationToken).ConfigureAwait(false);
                return itemInfo;
            }

            return null;
        }

        public async Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            if (item?.SiteId == null || item.Site != siteProvider.Site)
            {
                return false;
            }


            if (preloadItemStrategy == PreloadItemStrategy.Poster
                && item.Details.Rating != null)
            {
                return true;
            }

            string? section;
            if (item.Section == Section.Any)
            {
                section = item.Link?
                    .Segments
                    .Select(s => s.TrimEnd('/'))
                    .FirstOrDefault(s => s == "tv" || s == "movie");

                if (section == null)
                {
                    return false;
                }
            }
            else
            {
                section = item.Section.Modifier.HasFlag(SectionModifiers.Serial) ? "tv" : "movie";
            }

            var appendToResponse = "external_ids";
            if (preloadItemStrategy == PreloadItemStrategy.Full)
            {
                appendToResponse = "images,recommendations,credits,external_ids";
            }

            var json = await siteProvider
                .GetFromApiAsync(
                    $"{section}/{item.SiteId}",
                    new Dictionary<string, string?>
                    {
                        ["append_to_response"] = appendToResponse,
                        ["include_image_language"] = "en,ru,ua,null"
                    },
                    cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            if (json == null)
            {
                return false;
            }

            await FillItemInfoAsync(item, json, cancellationToken).ConfigureAwait(false);

            if (json["seasons"] is JArray seasonsJArray)
            {
                var seasons = seasonsJArray
                    .Select(s => s["season_number"]?.ToIntOrNull())
                    .Where(s => s.HasValue)
                    .Select(s => s!.Value)
                    .OrderByDescending(s => s)
                    .ToArray();
                item.Details.EpisodesCalendar = GetEpisodesCalendar(item.SiteId, seasons, default);
            }
            return true;
        }

        public async ValueTask<bool> IsAdultAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (!siteProvider.Properties.TryGetValue(TMDbSiteProvider.IgnoredKeywordsKey, out var ignoredKeywords)
                || ignoredKeywords == null)
            {
                return false;
            }

            var section = itemInfo.Section.Modifier.HasFlag(SectionModifiers.Serial) ? "tv" : "movie";
            var id = itemInfo.SiteId;

            var json = await siteProvider
                .GetFromApiAsync($"{section}/{id}/keywords", cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            if ((json?["results"] ?? json?["keywords"]) is not JArray results)
            {
                return false;
            }

            var adultKeywords = ignoredKeywords.Split(',').Select(kw => int.Parse(kw)).ToArray();

            return adultKeywords.Any(kw => results.Any(j => j["id"]?.ToIntOrNull() == kw));
        }

        private async IAsyncEnumerable<EpisodeInfo> GetEpisodesCalendar(
            string itemId, int[] seasons,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var season in seasons)
            {
                var json = await siteProvider
                    .GetFromApiAsync($"tv/{itemId}/season/{season}", cancellationToken)
                    .AsNewtonsoftJson<JObject>()
                    .ConfigureAwait(false);

                if (json?["episodes"] is not JArray episodesArray)
                {
                    yield break;
                }

                var episodes = episodesArray
                    .Select(ep => new EpisodeInfo
                    {
                        Season = season,
                        Episode = ep["episode_number"]?.ToIntOrNull(),
                        Title = ep["name"]?.ToString(),
                        DateTime = DateTimeOffset.TryParse(ep["air_date"]?.ToString(), out var temp) ? temp : (DateTimeOffset?)null
                    })
                    .OrderByDescending(ep => ep.Episode);
                foreach (var episode in episodes)
                {
                    yield return episode;
                }
            }
        }

        private async Task FillItemInfoAsync(ItemInfo itemInfo, JObject json, CancellationToken cancellationToken)
        {
            var isSerial = json["name"] != null;
            var section = isSerial ? "tv" : "movie";

            var isCartoon = json["genres"]?.Any(item => item["id"]?.ToString() == TMDbSiteProvider.CartoonId)
                ?? json["genre_ids"]?.Any(item => item?.ToString() == TMDbSiteProvider.CartoonId)
                ?? false;

            var tags = new[]
                {
                    GetTitledContainer(json, "genres", "name", TagType.Genre),
                    GetTitledContainer(json, "production_countries", "name", TagType.County)
                }.AsEnumerable();

            if (json["genre_ids"] is { } genresIdsJson)
            {
                var genresIds = genresIdsJson.Select(i => i.ToString()).ToArray();
                var movieGenres = await siteProvider.FetchGenresAsync(cacheService, false, cancellationToken).ConfigureAwait(false);
                var tvGenres = await siteProvider.FetchGenresAsync(cacheService, false, cancellationToken).ConfigureAwait(false);
                var genres = movieGenres.Union(tvGenres).Distinct();
                tags = tags
                    .Union(new[] {
                        new TagsContainer(TagType.Genre, genres.Where(i => genresIds.Any(id => id == i.Value)).ToArray())
                    });
            }
            var status = json["status"]?.ToString().Trim();

            itemInfo.Link = new Uri(TMDbSiteProvider.Domain, $"/{section}/{json["id"]}");
            itemInfo.Details.Year = json[isSerial ? "first_air_date" : "release_date"]?.ToString()?.Split('-')
                ?.FirstOrDefault()?.ToIntOrNull();
            itemInfo.Title = json[isSerial ? "name" : "title"]?.ToString().Trim();
            itemInfo.Details.TitleOrigin = json[isSerial ? "original_name" : "original_title"]?.ToString().Trim();
            itemInfo.Details.Description = json["overview"]?.ToString().Trim();
            itemInfo.Poster = TMDbSiteProvider.GetImage(json["poster_path"]?.ToString());

            var type = status != null && availableStatuses.TryGetValue(status, out var statusType)
                ? statusType
                : StatusType.Unknown;
            itemInfo.Details.Status = new Status(
                currentSeason: json["number_of_seasons"]?.ToIntOrNull(),
                currentEpisode: (json["seasons"] as JArray)?.LastOrDefault()?["episode_count"]?.ToIntOrNull(),
                type: type
            );

            if (json["vote_average"]?.ToDoubleOrNull() is double vote
                && vote > 0)
            {
                itemInfo.Details.Rating = new NumberBasedRating(10, vote, json["vote_count"]?.ToIntOrNull());
            }

            var itemModifier = (isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
                | (isCartoon ? SectionModifiers.Cartoon : SectionModifiers.None);
            itemInfo.Section = TMDbSiteProvider.Sections
                .First(s => s != Section.Any && s.Modifier.HasFlag(itemModifier));

            if (json["imdb_id"]?.ToString().Trim() is var imdbId
                && !string.IsNullOrEmpty(imdbId))
            {
                itemInfo.Details.LinkedIds[Sites.IMDb] = imdbId!;
            }

            if (json["images"] is JObject images)
            {
                itemInfo.Details.Images = images["backdrops"]?
                    .Select(i => TMDbSiteProvider.GetImage(i["file_path"]?.ToString()))
                    .Where(image => image.Count > 0)
                    .ToArray() ?? itemInfo.Details.Images;
            }

            if (json["recommendations"] is JObject recommendations)
            {
                itemInfo.Details.Similar = (await recommendations["results"]
                    .OfType<JObject>()
                    .ToAsyncEnumerable()
                    .SelectAwaitWithCancellation(CreateItemInfoFromJsonAsync)
                    .Where(item => item != null)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false))!;
            }

            if (json["external_ids"] is JObject externalIds)
            {
                foreach (var (site, id) in ParseExternalIds(externalIds))
                {
                    itemInfo.Details.LinkedIds[site] = id;
                }
            }

            if (json["credits"] is JObject credits)
            {
                var castContainer = GetTitledContainer(credits, "cast", "name", TagType.Actor, 5);
                var crew = credits["crew"]?
                    .Select(j => (Job: j["job"]?.ToString(), Name: j["name"]?.ToString(), Id: j["id"]?.ToString()))
                    .ToArray();

                var directors = crew?.Where(i => i.Job == "Director").Select(i => new TitledTag(i.Name, itemInfo.Site, "crew", i.Id)).ToArray();
                var directorsTitledContainer = directors?.Length > 0 ? new TagsContainer(TagType.Director, directors) : null;

                var writers = crew?.Where(i => i.Job == "Writer").Select(i => new TitledTag(i.Name, itemInfo.Site, "crew", i.Id)).ToArray();
                var writersTitledContainer = writers?.Length > 0 ? new TagsContainer(TagType.Writter, writers) : null;

                var music = crew?.Where(i => i.Job == "Music").Select(i => new TitledTag(i.Name, itemInfo.Site, "crew", i.Id)).ToArray();
                var musicTitledContainer = music?.Length > 0 ? new TagsContainer(TagType.Composer, music) : null;

                tags = tags.Union(new[] { castContainer, directorsTitledContainer, writersTitledContainer, musicTitledContainer });
            }
            itemInfo.Details.Tags = itemInfo.Details.Tags
                .Concat(tags.Where(t => t?.Items.Any() == true))
                .GroupBy(t => t!.TagType)
                .SelectMany(g => g.Key == TagType.Unknown
                    ? g.ToArray() : new[] { g.First() })
                .ToArray()!;
        }

        private static TagsContainer? GetTitledContainer(JObject json, string collectionNameField, string itemFieldName,
            TagType tagType, int limit = -1)
        {
            if (json[collectionNameField] == null)
            {
                return null;
            }

            var container = json[collectionNameField]
                .Where(j => j[itemFieldName]?.Type == JTokenType.String)
                .Select(j => TMDbSiteProvider.SupportedTagsForSearch.Contains(collectionNameField) && j["id"]?.ToString() is string value
                    ? new TitledTag(j[itemFieldName]!.ToString(), TMDbSiteProvider.SiteKey, collectionNameField, value)
                    : new TitledTag(j[itemFieldName]!.ToString()));

            if (limit > 0)
            {
                container = container.Take(limit);
            }

            return new TagsContainer(tagType, container.ToArray());
        }

        internal static IEnumerable<(Site Site, string Id)> ParseExternalIds(JObject json)
        {
            if (json["imdb_id"]?.ToString() is string imdbId
                && imdbId.Length > 0)
            {
                yield return (Sites.IMDb, imdbId);
            }
        }
    }
}

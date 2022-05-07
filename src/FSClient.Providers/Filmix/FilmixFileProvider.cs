namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class FilmixFileProvider : IFileProvider
    {
        private const string PlayerJsLink = "/modules/playerjs/playerjs.js";

        // Серия 1
        private static readonly Regex episodeRegex = new Regex(@"Серия (?<ep>\d{1,4})", RegexOptions.IgnoreCase);

        // Сезон 1
        private static readonly Regex seasonRegex = new Regex(@"Сезон (?<sn>\d{1,4})", RegexOptions.IgnoreCase);

        // s1e1
        private static readonly Regex idEpisodeRegex = new Regex(@"s(?<sn>\d{1,4})ep?(?<ep>\d{1,4})", RegexOptions.IgnoreCase);

        private Uri? currentItemPage;

        private readonly FilmixSiteProvider siteProvider;
        private readonly PlayerJsParserService playerJsParserService;

        public FilmixFileProvider(
            FilmixSiteProvider siteProvider,
            PlayerJsParserService playerJsParserService)
        {
            this.siteProvider = siteProvider;
            this.playerJsParserService = playerJsParserService;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => true;

        public bool ProvideTorrent => true;

        public bool ProvideTrailers => true;

        public bool EnforceProPlaylist { get; set; }

        public ProviderRequirements ReadRequirements => ProviderRequirements.AccountForSpecial | ProviderRequirements.ProForSpecial;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItemPage = items
                .FirstOrDefault()?
                .Link;
        }

        public async Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var siteId = itemInfo?.SiteId;
            if (siteId == null)
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var html = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "download/" + siteId))
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            var itemsDiv = html?.QuerySelector(".downloads .items");

            if (itemsDiv == null)
            {
                return null;
            }

            var filmItems = itemsDiv
                .QuerySelectorAll(".item")
                .Select(i =>
                {
                    var link = i
                        .QuerySelector(".download")?
                        .GetAttribute("onclick")?
                        .Split('=')
                        .Last()
                        .Trim('\'')
                        .ToUriOrNull(domain);
                    if (link == null)
                    {
                        return null;
                    }

                    var detItems = i
                            .QuerySelectorAll(".left .item-content")
                            .Select(n => n.PreviousElementSibling?.TextContent?.Trim() + " " + n.TextContent?.Trim())
                            .Where(m => !string.IsNullOrWhiteSpace(m));

                    var title = i.QuerySelector(".file-item")?.TextContent.Trim();

                    return new TorrentFolder(Site, $"fmx_t_{siteId}_f_{link.Segments.Last().Trim('/')}", link)
                    {
                        Title = title,
                        Details = string.Join("\n\r", detItems)
                    };
                })
                .Where(f => f != null);

            var nodes = new List<ITreeNode>();
            nodes.AddRange(filmItems!);

            var serialItems = itemsDiv
                .QuerySelectorAll(".translation-list")
                .Select(seas =>
                {
                    var seasTitle = seas
                        .PreviousElementSibling?
                        .QuerySelector(".season-title")?
                        .TextContent?
                        .Trim();
                    var seasonNumber = seasTitle?.Split(' ').First().GetDigits().ToIntOrNull();
                    var season = new Folder(siteProvider.Site, $"fmx_t_{siteId}_s_{seasonNumber}", FolderType.Season, PositionBehavior.Max)
                    {
                        Title = seasTitle,
                        Season = seasonNumber
                    };

                    var translateNum = 0;
                    var translations = seas
                        .QuerySelectorAll(".series-list")
                        .Select(tran =>
                        {
                            var translate = new Folder(siteProvider.Site, season.Id + "_" + translateNum++, FolderType.Translate, PositionBehavior.Average)
                            {
                                Title = tran
                                    .PreviousElementSibling?
                                    .QuerySelector(".translation-title")?
                                    .TextContent,
                                Details = tran
                                    .PreviousElementSibling?
                                    .QuerySelector(".count-files")?
                                    .TextContent
                            };
                            var files = tran
                                .QuerySelectorAll(".series")
                                .Select(s =>
                                {
                                    if (!Uri.TryCreate(domain, s.QuerySelector(".download")?.GetAttribute("href"), out var link))
                                    {
                                        return null;
                                    }

                                    var title = s.QuerySelector(".origin-name")?.TextContent;
                                    var seriesInfo = s.QuerySelector(".series-title")?.TextContent;
                                    var quality = s.QuerySelector(".quality")?.TextContent;
                                    var size = s.QuerySelector("size-series")?.TextContent;

                                    Range? episode = null;
                                    var separatorIndex = seriesInfo?.LastIndexOf(' ') ?? -1;
                                    if (separatorIndex > 0
                                        && RangeExtensions.TryParse(seriesInfo?.Substring(0, separatorIndex), out var ep))
                                    {
                                        episode = ep;
                                    }
                                    else if (!string.IsNullOrWhiteSpace(seriesInfo))
                                    {
                                        title = seriesInfo + title;
                                    }

                                    var detItems = s
                                        .NextElementSibling?
                                        .QuerySelectorAll(".torrent-media")
                                        .Select(m => m.TextContent?.Trim())
                                        .Where(m => !string.IsNullOrWhiteSpace(m))
                                        ?? Enumerable.Empty<string>();

                                    return new TorrentFolder(Site, translate.Id + "_" + link.Segments.Last().Trim('/'), link)
                                    {
                                        Episode = episode,
                                        Title = title,
                                        Size = size,
                                        Details = string.Join("\n\r", detItems)
                                    };
                                })
                                .Where(t => t?.Link != null)
                                .Cast<ITreeNode>()
                                .ToList();

                            translate.AddRange(files);
                            return translate;
                        })
                        .Where(f => f.Count > 0)
                        .Cast<ITreeNode>()
                        .ToList();

                    season.AddRange(translations);
                    return season;
                })
                .Where(f => f.Count > 0)
                .Cast<ITreeNode>()
                .ToList();

            serialItems.SortStrings(i => i.Title ?? string.Empty);

            nodes.AddRange(serialItems);

            var folder = new Folder(Site, $"fmx_t_{siteId}", FolderType.Item, PositionBehavior.Average);
            folder.AddRange(nodes);
            return folder;
        }

        public async Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var currentItemPage = itemInfo?.Link;
            if (currentItemPage == null)
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            if (!currentItemPage.IsAbsoluteUri)
            {
                currentItemPage = new Uri(domain, currentItemPage);
            }

            var html = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(currentItemPage.ToString().Replace("play", "trailers")))
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            if (html == null)
            {
                return null;
            }

            string? trailer_id = null;
            string? trailerTitlePlayer = null;
            string? trailerVideoLink5 = null;

            foreach (var script in html.Scripts.Reverse())
            {
                var matches = Regex.Matches(script.Text, @"(?<key>trailer_id|trailerTitlePlayer|trailerVideoLink5)\s*(?:=|:)\s*(?:'|"")?(?<value>[^'"",;]*)", RegexOptions.Compiled);
                foreach (Match? match in matches)
                {
                    if (match!.Groups.Count < 3)
                    {
                        continue;
                    }

                    switch (match.Groups["key"]?.Value)
                    {
                        case "trailer_id":
                            trailer_id = match.Groups["value"]?.Value;
                            break;
                        case "trailerTitlePlayer":
                            trailerTitlePlayer = match.Groups["value"]?.Value;
                            break;
                        case "trailerVideoLink5":
                            trailerVideoLink5 = match.Groups["value"]?.Value;
                            break;
                    }
                }
                if (!string.IsNullOrEmpty(trailerVideoLink5))
                {
                    var decoded = await DecodePlayerJsAsync(
                        new Uri(currentItemPage, PlayerJsLink),
                        trailerVideoLink5,
                        cancellationToken).ConfigureAwait(false);
                    if (decoded != null)
                    {
                        trailerVideoLink5 = decoded;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(trailerVideoLink5))
            {
                return null;
            }

            var videos = ParseVideosFromLink(currentItemPage, trailerVideoLink5!).ToArray();
            if (videos.Length == 0)
            {
                return null;
            }

            var file = new File(Site, $"fmx_tr_{itemInfo!.SiteId}_{trailer_id}_t")
            {
                Title = trailerTitlePlayer,
                IsTrailer = true
            };
            file.SetVideos(videos);

            var folder = new Folder(Site, $"fmx_tr_{itemInfo!.SiteId}", FolderType.Item, PositionBehavior.Average);
            folder.Add(file);
            return folder;
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            if (currentItemPage == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (!currentItemPage.IsAbsoluteUri)
            {
                var domain = await siteProvider.GetMirrorAsync(token).ConfigureAwait(false);
                currentItemPage = new Uri(domain, currentItemPage);
            }

            if (folder is FilmixFolder filmixFolder)
            {
                if (folder.Id == null)
                {
                    return Enumerable.Empty<ITreeNode>();
                }
                var items = await LoadFolderAsync(filmixFolder.Id, filmixFolder.DataLink, currentItemPage, token).ConfigureAwait(false);

                return items;
            }
            else if (folder.Count == 0)
            {
                var items = await LoadTranslatesAsync(currentItemPage, token)
                    .ToArrayAsync()
                    .ConfigureAwait(false);
                if (items.Length == 1
                    && items[0] is FilmixFolder singleFolder)
                {
                    return await GetFolderChildrenAsync(singleFolder, token).ConfigureAwait(false);
                }

                return items;
            }

            return Enumerable.Empty<ITreeNode>();
        }

        private async Task<IEnumerable<ITreeNode>> LoadTranslatesAsync(Uri link, CancellationToken token)
        {
            var rootId = link.Segments.LastOrDefault()?.Trim('/').Split('-').FirstOrDefault();
            if (rootId == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var playerData = await GetPlayerDataFromApiAsync(rootId, link, token)
                .ConfigureAwait(false);
            if (playerData == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            // Additional condition should be removed after pro playlist will be supported
            if (EnforceProPlaylist)
            {
                if (playerData["links"] is JArray links)
                {
                    var proPlaylist = ParseProPlaylist(links, link, rootId).ToArray();
                    if (proPlaylist.Length > 0)
                    {
                        return proPlaylist;
                    }
                    else if (EnforceProPlaylist)
                    {
                        return Enumerable.Empty<ITreeNode>();
                    }
                }
                else if (EnforceProPlaylist)
                {
                    return Enumerable.Empty<ITreeNode>();
                }
            }

            var translates = playerData["translations"]?["video"]?
                .Children()
                .Cast<JProperty>()
                .Select(p => (key: p.Name, value: p.Value?.ToString()))
                .Where(p => p.value != null)
                .ToArray();

            if (translates == null
                || translates.Length == 0)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            return await translates
                .Select((tuple, index) => (tuple.key, tuple.value, index))
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation(async (tuple, ct) =>
                {
                    var id = $"{rootId}_{tuple.index}";
                    var title = tuple.key;
                    var dataLink = await DecodePlayerJsAsync(
                        new Uri(link, PlayerJsLink),
                        tuple.value,
                        ct).ConfigureAwait(false);
                    if (dataLink == null)
                    {
                        return null;
                    }

                    if (title == null
                        || title.IndexOf("Плеер", StringComparison.Ordinal) == 0)
                    {
                        title = "Неизвестный перевод";
                    }

                    var videos = ParseVideosFromLink(link, dataLink).ToArray();

                    if (videos.Length > 0)
                    {
                        var file = new File(Site, id)
                        {
                            FrameLink = link,
                            Title = RemoveHDCaptions(title)
                        };
                        SetFilmixVideo(file, videos);
                        return (ITreeNode)file;
                    }

                    var dataLinkUri = dataLink.ToUriOrNull(link);
                    if (dataLinkUri == null)
                    {
                        return null;
                    }

                    return new FilmixFolder(siteProvider.Site, id, dataLinkUri, FolderType.Translate, PositionBehavior.Average)
                    {
                        Title = title
                    };
                })
                .Where(node => node != null)!
                .OfType<ITreeNode>()
                .ToListAsync(token)
                .ConfigureAwait(false);
        }

        private async Task<JObject?> GetPlayerDataFromApiAsync(
            string rootId, Uri referer, CancellationToken token)
        {
            var domain = await siteProvider.GetMirrorAsync(token).ConfigureAwait(false);

            if (!siteProvider.Handler.GetCookies(domain, "FILMIXNET").Any())
            {
                await siteProvider.HttpClient
                    .GetBuilder(domain)
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .SendAsync(token)
                    .ConfigureAwait(false);
            }

            var json = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "api/movies/player_data"))
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .WithBody(new Dictionary<string, string>
                {
                    ["post_id"] = rootId,
                    ["showfull"] = "false"
                })
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", referer.ToString())
                .WithAjax()
                .SendAsync(token)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            return json?["message"] as JObject;
        }

        private IEnumerable<Video> ParseVideosFromLink(Uri domain, string fileLink)
        {
            if (fileLink.EndsWith("txt", StringComparison.Ordinal))
            {
                return Enumerable.Empty<Video>();
            }

            if (!fileLink.StartsWith("[", StringComparison.Ordinal) && fileLink.Contains('[') && fileLink.Contains(']'))
            {
                return ParseVideosFromOldLinkFormat(fileLink);
            }

            if (!siteProvider.Properties.TryGetValue(FilmixSiteProvider.FullHDOnlyForProKey, out var fullHDOnlyForProStr)
                || !bool.TryParse(fullHDOnlyForProStr, out var fullHDOnlyForPro))
            {
                fullHDOnlyForPro = false;
            }

            var allowFullHD = !fullHDOnlyForPro || (siteProvider.CurrentUser?.HasProStatus ?? false);

            return ParseVideosFromNewLinkFormat(domain, fileLink, allowFullHD);
        }

        private static IEnumerable<Video> ParseVideosFromOldLinkFormat(string fileLink)
        {
            var startCut = fileLink.LastIndexOf('[') + 1;
            var endCut = fileLink.LastIndexOf(']');

            var qualities = fileLink[startCut..endCut]
               .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var quality in qualities)
            {
                if (quality.EndsWith("p", StringComparison.Ordinal))
                {
                    continue;
                }

                var uriStr = fileLink.Substring(0, startCut - 1)
                             + quality
                             + fileLink.Substring(endCut + 1, fileLink.Length - endCut - 1);
                if (Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
                {
                    yield return new Video(uri)
                    {
                        Quality = quality
                    };
                }
            }
        }

        private static IEnumerable<Video> ParseVideosFromNewLinkFormat(Uri domain, string fileLink, bool allowFullHD)
        {
            return ProviderHelper.ParsePlayerJsKeyValuePairs(fileLink)
                .DistinctBy(tuple => tuple.value)
                .Where(tuple => tuple.value.EndsWith("p.mp4", StringComparison.Ordinal) == false)
                .Select(tuple => (
                    quality: tuple.key
                        .Replace("UHD", "")
                        .Replace("HD", "")
                        .Trim(),
                    success: Uri.TryCreate(domain, tuple.value, out var link),
                    link
                ))
                .Where(tuple => tuple.success)
                .GroupBy(tuple => tuple.quality)
                .Select(g => new Video(g.Select(l => new VideoVariant(l.link!)))
                {
                    Quality = g.Key
                })
                .Where(v => allowFullHD || v?.Quality < 1080)!;
        }

        private async Task<IEnumerable<ITreeNode>> LoadFolderAsync(string id, Uri dataLink, Uri filmixPage, CancellationToken token)
        {
            var nodes = new List<ITreeNode>();

            var respText = await siteProvider
                .HttpClient
                .GetBuilder(dataLink)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .WithAjax()
                .SendAsync(token)
                .AsText()
                .ConfigureAwait(false);

            var parsedText = await DecodePlayerJsAsync(
                new Uri(filmixPage, PlayerJsLink),
                respText,
                token).ConfigureAwait(false);
            if (parsedText == null)
            {
                return nodes;
            }

            return await ParsePlaylistAsync(JsonHelper.ParseOrNull<JToken>(parsedText), filmixPage, id)
                .ToListAsync(token)
                .ConfigureAwait(false);
        }

        private IAsyncEnumerable<ITreeNode> ParsePlaylistAsync(
            JToken? json, Uri filmixPage, string parentId)
        {
            var id = 0;
            return ((json as JArray)
                ?? (json as JObject)?["folder"]
                ?? (json as JObject)?["playlist"]
                ?? new JArray())?
                .Children()
                .OfType<JObject>()
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation(async (obj, ct) =>
                {
                    var fileLink = await DecodePlayerJsAsync(
                        new Uri(filmixPage, PlayerJsLink),
                        obj["file"]?.ToString(),
                        ct).ConfigureAwait(false);
                    if (fileLink != null)
                    {
                        var title = (obj["title"] ?? obj["comment"])?.ToString();

                        int? episode = null, season = null;
                        if (int.TryParse(obj["serieId"]?.ToString(), out var series))
                        {
                            episode = series;
                            season = obj["season"]?.ToIntOrNull();
                        }
                        else if (obj["id"]?.ToString() is string tokenId
                            && idEpisodeRegex.Match(tokenId) is var idMatch
                            && idMatch.Success)
                        {
                            episode = idMatch.Groups["ep"].Value.ToIntOrNull();
                            season = idMatch.Groups["sn"].Value.ToIntOrNull();
                        }
                        else if (title != null
                            && episodeRegex.Match(title) is var episodeMatch
                            && seasonRegex.Match(title) is var seasonMatch
                            && episodeMatch.Success
                            && seasonMatch.Success)
                        {
                            episode = episodeMatch.Groups["ep"].Value.ToIntOrNull();
                            season = seasonMatch.Groups["sn"].Value.ToIntOrNull();
                        }
                        else
                        {
                            title = RemoveHDCaptions(title?.ToString().Trim());
                        }

                        var file = new File(Site, $"{parentId}_{episode}")
                        {
                            FrameLink = filmixPage,
                            Title = episode.HasValue ? null : title,
                            Episode = episode.ToRange(),
                            Season = season
                        };
                        var videos = ParseVideosFromLink(filmixPage, fileLink).ToArray();
                        SetFilmixVideo(file, videos);

                        return file;
                    }

                    if (obj["playlist"] != null || obj["folder"] != null)
                    {
                        var tokenId = $"{parentId}_{id++}";
                        var title = (obj["title"] ?? obj["comment"])?.ToString();
                        var childs = await ParsePlaylistAsync(obj, filmixPage, tokenId)
                            .ToListAsync(ct)
                            .ConfigureAwait(false);
                        var season = seasonRegex.Match(title ?? "").Groups["sn"].Value.ToIntOrNull();

                        var folder = new Folder(siteProvider.Site, tokenId, FolderType.Unknown, PositionBehavior.Average)
                        {
                            Title = title,
                            Season = season
                        };
                        folder.AddRange(childs);

                        return (ITreeNode)folder;
                    }
                    return null;
                })
                .Where(n => n != null)!;
        }

        private IEnumerable<ITreeNode> ParseProPlaylist(
            JArray links, Uri filmixPage, string parentId)
        {
            return links
                .OfType<JObject>()
                .Select((translateObject, translateIndex) =>
                {
                    var translateId = $"{parentId}_{translateIndex}";
                    var title = translateObject["name"]?.ToString();

                    if (translateObject["files"] is JObject seasons)
                    {
                        var translateFolder = new Folder(Site, translateId, FolderType.Translate, PositionBehavior.Average)
                        {
                            Title = title
                        };

                        translateFolder.AddRange(seasons
                            .OfType<JProperty>()
                            .Select((seasonProperty, seasonIndex) => (
                                seasonObject: seasonProperty.Value as JObject,
                                seasonNumber: seasonRegex.Match(seasonProperty.Name)
                                    .Groups["sn"].Value
                                    .ToIntOrNull() ?? (seasonIndex + 1),
                                seasonIndex: seasonIndex
                            ))
                            .Where(t => t.seasonObject != null)
                            .Select(snTuple =>
                            {
                                var seasonId = $"{translateId}_{snTuple.seasonIndex}";

                                var episodes = snTuple.seasonObject!
                                    .OfType<JProperty>()
                                    .Select((episodeProperty, episodeIndex) => (
                                        episodeArray: episodeProperty.Value as JArray,
                                        episodeNumber: episodeRegex.Match(episodeProperty.Name)
                                            .Groups["ep"].Value
                                            .ToIntOrNull() ?? (episodeIndex + 1)
                                    ))
                                    .Where(t => t.episodeArray != null)
                                    .Select(epTuple => ParseFile(
                                        epTuple.episodeArray!, seasonId, snTuple.seasonNumber, epTuple.episodeNumber, null))
                                    .Where(file => file.Videos.Count > 0)
                                    .ToArray();

                                var seasonFolder = new Folder(Site, seasonId, FolderType.Season, PositionBehavior.Average)
                                {
                                    Season = snTuple.seasonNumber,
                                    Title = "Сезон " + snTuple.seasonNumber
                                };
                                seasonFolder.AddRange(episodes);
                                return seasonFolder;
                            })
                            .Where(seasonFolder => seasonFolder.Count > 0));

                        if (translateFolder.Count == 0)
                        {
                            return null;
                        }

                        return translateFolder;
                    }
                    else if (translateObject["files"] is JArray videos)
                    {
                        var file = ParseFile(videos, translateId, null, null, title);
                        if (file.Videos.Count == 0)
                        {
                            return null;
                        }
                        return (ITreeNode)file;
                    }
                    return null;
                })
                .Where(node => node != null)!;

            File ParseFile(JArray episodeArray, string rootId, int? seasonNumber, int? episodeNumber, string? title)
            {
                var file = new File(Site, $"{rootId}_{episodeNumber}")
                {
                    Season = seasonNumber,
                    Episode = episodeNumber.ToRange(),
                    FrameLink = filmixPage,
                    Title = title
                };

                file.SetVideos(episodeArray
                    .OfType<JObject>()
                    .Select(videoObject => (
                        quality: (Quality)(videoObject["quality"]?.ToIntOrNull() ?? 0),
                        link: videoObject["url"]?.ToUriOrNull(filmixPage),
                        size: videoObject["size"]?.ToLongOrNull()
                    ))
                    .Where(tuple => tuple.link != null && !tuple.quality.IsUnknown)
                    .Select(tuple => new Video(tuple.link!)
                    {
                        Quality = tuple.quality,
                        Size = tuple.size,
                        CustomHeaders =
                        {
                            ["Referer"] = filmixPage.ToString(),
                            ["User-Agent"] = WebHelper.DefaultUserAgent
                        }
                    })
                    .ToArray());

                return file;
            }
        }

        private ValueTask<string?> DecodePlayerJsAsync(Uri playerJsFileLink, string? input, CancellationToken cancellationToken)
        {
            return playerJsParserService.DecodeAsync(
                input,
                new PlayerJsConfig(
                    playerJsFileLink: siteProvider.PlayerJsConfig.PlayerJsFileLink ?? playerJsFileLink,
                    keys: siteProvider.PlayerJsConfig.Keys,
                    separator: siteProvider.PlayerJsConfig.Separator,
                    oyKey: siteProvider.PlayerJsConfig.OyKey),
                cancellationToken);
        }

        private void SetFilmixVideo(File file, Video[] videos)
        {
            var currentHour = DateTime.UtcNow.Hour;
            if (currentHour > 18 && currentHour < 22
                && siteProvider.CurrentUser == null)
            {
                file.SetVideosFactory(async (_, cancellationToken) => (await Task
                    .WhenAll(videos
                        .Select(async v =>
                        {
                            var totalLength = !v.Quality.IsHD ? (long?)null : (await Task
                                    .WhenAll(v
                                        .Links
                                        .Select(l => siteProvider.HttpClient.GetContentSizeAsync(l, v.CustomHeaders, cancellationToken)))
                                    .ConfigureAwait(false))
                                .Sum();

                            return (video: v, totalLength);
                        }))
                    .ConfigureAwait(false))
                    .Where(t => t.totalLength == null || t.totalLength > 5 * 1024 * 1024)
                    .Select(t => t.video));
            }
            else
            {
                file.SetVideos(videos);
            }
        }

        private static string? RemoveHDCaptions(string? input)
        {
            if (input == null)
            {
                return null;
            }

            try
            {
                return Regex
                    .Replace(
                        input,
                        @"(\[(?<l>.*?)(?<m>((\bFull)?\s?HD\b)|([24][KК]\b)|(\d{3-4}p\b)),?(?<r>.*?)])",
                        m => "[" + string
                            .Join(", ", new[] { m.Groups["l"].Value, m.Groups["r"].Value }
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s.Trim())) + "]",
                        RegexOptions.IgnoreCase)
                    .Replace("[]", "")
                    .Trim();
            }
            catch
            {
                return input;
            }
        }

        public class FilmixFolder : Folder
        {
            public FilmixFolder(Site site, string id, Uri dataLink, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                DataLink = dataLink;
            }

            public Uri DataLink { get; }
        }
    }
}

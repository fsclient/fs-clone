namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Html.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class SeasonVarFileProvider : IFileProvider
    {
        private const string DefaultSecureKey = "6";
        private const string DefaultTime = "1575325411";

        private IEnumerable<ItemInfo>? currentItems;
        private readonly SeasonVarSiteProvider siteProvider;
        private readonly PlayerJsParserService playerJsParserService;

        public SeasonVarFileProvider(
            SeasonVarSiteProvider siteProvider,
            PlayerJsParserService playerJsParserService)
        {
            this.siteProvider = siteProvider;
            this.playerJsParserService = playerJsParserService;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.ProForSpecial;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItems = items.ToArray();
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            List<ITreeNode> items;

            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (folder is SeasonVarFolder seasonVarFolder)
            {
                items = seasonVarFolder.PlaylistLink != null
                    ? await GetFilesAsync(seasonVarFolder.Id, seasonVarFolder.Link, seasonVarFolder.PlaylistLink, seasonVarFolder.Season, seasonVarFolder.IsTrailers, token).ConfigureAwait(false)
                    : await ParseSeasonAsync(seasonVarFolder.Id, seasonVarFolder.Link, seasonVarFolder.Season, token).ConfigureAwait(false);
                return items;
            }
            else if (folder.Count == 0 && currentItems?.Any() == true)
            {
                items = currentItems
                    .Select(i => new SeasonVarFolder(Site, "sv" + i!.SiteId, i.Link!, FolderType.Season, PositionBehavior.Average)
                    {
                        Title = i.Details.Status.CurrentSeason.HasValue ? "Сезон " + i.Details.Status.CurrentSeason : i.Title,
                        Season = i.Details.Status.CurrentSeason
                    })
                    .Cast<ITreeNode>()
                    .ToList();
                if (items.Count == 1
                    && items[0] is Folder singleFolder)
                {
                    return await GetFolderChildrenAsync(singleFolder, token).ConfigureAwait(false);
                }
                else
                {
                    return items;
                }
            }

            return Enumerable.Empty<ITreeNode>();
        }

        // var pl = {'0': "/playls2/7a5d934ed1aef114df3d7ff47a8a969d8/trans/15555/list.xml?time=1493926911"};
        private static readonly Regex initPlRegex = new Regex(@"pl\s*=\s*{\s*'(?<key>[^']+)'\s*:\s*""(?<url>[^""]+)""\s*}");

        // pl[68] = "/playls2/7a5d934ed1aef114df3d7ff47a8a969d8/trans%D0%A2%D1%80%D0%B5%D0%B9%D0%BB%D0%B5%D1%80%D1%8B/15555/list.xml?time=1493926911";
        private static readonly Regex elsePlRegex = new Regex(@"pl\[(?<key>[^\]]+)]\s*=\s*""(?<url>[^""]+)""");

        private async Task<List<ITreeNode>> ParseSeasonAsync(string parentId, Uri link, int? season, CancellationToken token)
        {
            var html = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(link, "player.php"))
                .WithBody(new Dictionary<string, string>
                {
                    ["id"] = parentId.Length > 2 ? parentId[2..] : parentId,
                    ["secure"] = DefaultSecureKey,
                    ["time"] = DefaultTime,
                    ["type"] = "html5",
                })
                .WithHeader("Cookie", "playerHtml=true;premAll=1")
                .WithAjax()
                .SendAsync(token)
                .AsHtml(token).ConfigureAwait(false);
            if (html == null)
            {
                return new List<ITreeNode>();
            }

            var transDict = new Dictionary<string, string>();

            foreach (var script in html.Scripts)
            {
                var initMatch = initPlRegex.Match(script.Text);
                if (initMatch.Success && initMatch.Groups.Count > 2)
                {
                    var key = initMatch.Groups["key"].Value;
                    var url = initMatch.Groups["url"].Value;
                    if (transDict.ContainsKey(key))
                    {
                        transDict[key] = url;
                    }
                    else
                    {
                        transDict.Add(key, url);
                    }
                }

                foreach (Match? match in elsePlRegex.Matches(script.Text))
                {
                    if (match!.Success && match.Groups.Count > 2)
                    {
                        var key = match.Groups["key"].Value;
                        var url = match.Groups["url"].Value;
                        if (transDict.ContainsKey(key))
                        {
                            transDict[key] = url;
                        }
                        else
                        {
                            transDict.Add(key, url);
                        }
                    }
                }
            }

            var translates = new List<ITreeNode>();

            var items = html
                .QuerySelectorAll(".pgs-trans li")
                .Select(li => (
                    key: li.GetAttribute("data-translate"),
                    title: li.TextContent?.Trim()))
                .Where(i => !string.IsNullOrEmpty(i.key))
                .ToArray();

            if (items.Length == 0)
            {
                items = new[] { ("0", "Стандартный") }!;
            }

            foreach (var (key, title) in items)
            {
                if (!transDict.ContainsKey(key!)
                    || link == null
                    || !Uri.TryCreate(link, transDict[key!]?.TrimStart('/'), out var translateLink))
                {
                    continue;
                }

                var positionBehavior = title == "Трейлеры"
                    ? PositionBehavior.None
                    : PositionBehavior.Average;

                translates.Add(new SeasonVarFolder(Site, parentId + "_" + key, link, FolderType.Translate, positionBehavior)
                {
                    Title = title,
                    PlaylistLink = translateLink,
                    Season = season,
                    IsTrailers = title == "Трейлеры"
                });
            }
            if (translates.Count == 1)
            {
                var folder = ((SeasonVarFolder)translates[0]);
                return await GetFilesAsync(folder.Id, folder.Link, folder.PlaylistLink!, folder.Season, folder.IsTrailers, token).ConfigureAwait(false);
            }
            return translates;
        }

        private async Task<List<ITreeNode>> GetFilesAsync(string parentId, Uri pageLink, Uri playlistLink, int? season, bool? isTrailers, CancellationToken token)
        {
            if (siteProvider.CurrentUser == null
                || playlistLink.Host.Contains("seasonhit-api"))
            {
                playlistLink = new Uri(playlistLink.ToString().Replace("plist.txt", "list.xml"));
            }

            var playlistJson = await siteProvider
                .HttpClient
                .GetBuilder(playlistLink)
                .WithAjax()
                .SendAsync(token)
                .AsNewtonsoftJson<JToken>()
                .ConfigureAwait(false);

            return ParsePlaylist(playlistJson, pageLink, parentId, season, isTrailers);
        }

        private List<ITreeNode> ParsePlaylist(JToken? json, Uri playlistLink, string parentId, int? season, bool? isTrailers)
        {
            var nodes = new List<ITreeNode>();
            if (json == null)
            {
                return nodes;
            }

            var tokens = (json is JObject jObj && jObj.TryGetValue("playlist", out var playlistObj))
                ? playlistObj.Children().ToList()
                : json.Children().ToList();

            var id = 0;

            foreach (var token in tokens)
            {
                var tokenId = parentId + "_" + id++;
                if (token["file"] is { } fileJson)
                {
                    var fileStr = fileJson.ToString();

                    fileStr = playerJsParserService.DecodeAsync(fileStr, siteProvider.PlayerJsConfig, default).AsTask().GetAwaiter().GetResult();

                    var fileLinks = fileStr?.Split(new[] { " or " }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => (
                            valid: Uri.TryCreate(playlistLink, l, out var link),
                            link
                        ))
                        .Where(t => t.valid)
                        .Select(t => t.link!)
                        .ToArray() ?? Array.Empty<Uri>();

                    if (fileLinks.Length == 0)
                    {
                        continue;
                    }

                    var title = (token["title"] ?? token["comment"])?.ToString().Replace("<br>", " ");
                    var titleParts = title?.Split(' ');

                    var file = new File(Site, tokenId)
                    {
                        Season = season,
                        FrameLink = playlistLink,
                        IsTrailer = isTrailers ?? false
                    };

                    var subsToken = (token["sub"] ?? token["subtitle"])?.ToString();
                    if (subsToken != null)
                    {
                        var pairs = ProviderHelper.ParsePlayerJsKeyValuePairs(subsToken);
                        file.SubtitleTracks.AddRange(pairs
                            .Select(tuple => (
                                lang: LocalizationHelper.NormalizeLanguageName(tuple.key.NotEmptyOrNull()) ?? LocalizationHelper.RuLang,
                                link: tuple.value.ToUriOrNull(playlistLink)
                            ))
                            .Where(tuple => tuple.link != null)
                            .Select(tuple => new SubtitleTrack(tuple.lang, tuple.link!))
                            .ToArray());
                    }

                    if (titleParts?.Length > 0
                        && RangeExtensions.TryParse(titleParts.FirstOrDefault(), out var range))
                    {
                        file.Episode = range;
                    }
                    else
                    {
                        file.Title = title;
                    }

                    var sdLink = fileLinks.FirstOrDefault(l => l.AbsoluteUri.Contains("/7f_")) ?? fileLinks.FirstOrDefault();
                    var hdLink = fileLinks.FirstOrDefault(l => l != sdLink && l.AbsoluteUri.Contains("/hd_"));

                    if (sdLink != null)
                    {
                        if (hdLink != null)
                        {
                            var hdVideo = new Video(hdLink)
                            {
                                Quality = 720
                            };
                            var sdVideo = new Video(sdLink)
                            {
                                Quality = 480
                            };

                            file.SetVideos(hdVideo, sdVideo);
                        }
                        else
                        {
                            var sdVideo = new Video(sdLink)
                            {
                                Quality = 480
                            };

                            file.SetVideos(sdVideo);
                        }

                        nodes.Add(file);
                    }
                }
                else if (token["playlist"] != null)
                {
                    var folder = new Folder(Site, tokenId, FolderType.Unknown, PositionBehavior.Average)
                    {
                        Title = token["comment"]?.ToString()
                    };
                    folder.AddRange(ParsePlaylist(token, playlistLink, tokenId, season, isTrailers));

                    nodes.Add(folder);
                }
            }

            return nodes;
        }

        public static string? GetSecureKey(IHtmlDocument? html)
        {
            if (html == null)
            {
                return null;
            }

            var secureRegex = new Regex(@"secureMark.*(?::|=)\s*(?:'|"")([^ '""]+).");
            return html
                .Scripts
                .Select(s => secureRegex.Match(s.Text))
                .FirstOrDefault(m => m.Success && m.Groups.Count > 1)?
                .Groups[1]?
                .Value;
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public class SeasonVarFolder : Folder
        {
            public SeasonVarFolder(Site site, string id, Uri link, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                Link = link;
            }

            public Uri Link { get; }
            public Uri? PlaylistLink { get; set; }
            public bool? IsTrailers { get; set; }
        }
    }
}

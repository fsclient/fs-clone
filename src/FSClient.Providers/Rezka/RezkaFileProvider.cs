namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class RezkaFileProvider : IFileProvider
    {
        private ItemInfo? currentItem;

        private readonly RezkaSiteProvider siteProvider;
        private readonly IPlayerParseManager playerParseManager;

        public RezkaFileProvider(
            RezkaSiteProvider siteProvider,
            IPlayerParseManager playerParseManager)
        {
            this.siteProvider = siteProvider;
            this.playerParseManager = playerParseManager;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItem = items
                .FirstOrDefault();
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (folder is RezkaFolder rezkaFolder)
            {
                var id = rezkaFolder.Id.Substring(3).SplitLazy(2, '_').First();
                var items = await GetSeasonNodesAsync(id, rezkaFolder.TranslatorId, rezkaFolder.ItemTitle, token).ConfigureAwait(false);
                return items;
            }
            else if (folder.Count == 0
                && currentItem?.Link is Uri link
                && currentItem.SiteId != null)
            {
                var items = await GetOnlineNodesTree(link, currentItem.SiteId, currentItem.Title, token).ConfigureAwait(false);
                return items;
            }

            return Enumerable.Empty<ITreeNode>();
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (itemInfo?.SiteId is not string id)
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var iframeCode = (await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "/engine/ajax/gettrailervideo.php"))
                .WithBody(new Dictionary<string, string>
                {
                    ["id"] = id
                })
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", domain.ToString())
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false))?
                ["code"]?
                .ToString();
            if (iframeCode == null)
            {
                return null;
            }

            var iframeElement = WebHelper.ParseHtml(iframeCode);
            var iframeSrc = iframeElement?.QuerySelector("iframe")?.GetAttribute("src")?.ToUriOrNull(domain);
            if (iframeSrc == null
                || (!iframeSrc.Host.Contains("youtu")
                && !iframeSrc.Host.Contains("rezka")))
            {
                return null;
            }

            if (playerParseManager.CanOpenFromLinkOrHostingName(iframeSrc, Sites.Youtube))
            {
                var ytFile = await playerParseManager
                    .ParseFromUriAsync(iframeSrc, Sites.Youtube, cancellationToken).ConfigureAwait(false);
                if (ytFile == null)
                {
                    return null;
                }
                ytFile.IsTrailer = true;
                ytFile.Title ??= itemInfo.Title;
                ytFile.ItemTitle = itemInfo.Title;

                var ytFolder = new Folder(Site, $"rzk_t_{id}", FolderType.Item, PositionBehavior.Average);
                ytFolder.Add(ytFile);
                return ytFolder;
            }

            var file = new File(Site, $"rzk{id}_t")
            {
                Title = "Трейлер",
                IsTrailer = true,
                FrameLink = iframeSrc,
                ItemTitle = itemInfo.Title
            };
            file.SetVideosFactory(async (file, ct) =>
            {
                var pageText = await siteProvider.HttpClient
                    .GetBuilder(iframeSrc)
                    .SendAsync(ct)
                    .AsText()
                    .ConfigureAwait(false) ?? string.Empty;
                var match = Regex.Match(pageText, @"set_video_player\(.*?'(?<sd>[^']*?)',\s*'(?<hd>[^']*?)',\s*'(?<sub>[^']*?)'\)");

                if (match.Groups["sub"].Value.ToUriOrNull(domain) is Uri sub)
                {
                    file.SubtitleTracks.Add(new SubtitleTrack(LocalizationHelper.RuLang, sub));
                }

                var videos = new List<Video>();
                if (match.Groups["hd"].Value.ToUriOrNull(domain) is Uri hd)
                {
                    videos.Add(new Video(hd)
                    {
                        Quality = 720
                    });
                }
                if (match.Groups["sd"].Value.ToUriOrNull(domain) is Uri sd)
                {
                    videos.Add(new Video(sd)
                    {
                        Quality = 480
                    });
                }
                return videos.ToArray();
            });

            var folder = new Folder(Site, $"rzk_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
            folder.Add(file);
            return folder;
        }

        private async Task<IEnumerable<ITreeNode>> GetOnlineNodesTree(Uri link, string itemInfoId, string? itemTitle,
            CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var html = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, link))
                .WithHeader("Referer", link.ToString())
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (html == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var isFilm = html.QuerySelector(".b-simple_episode__item") == null;
            ICollection<ITreeNode> nodes;

            if (isFilm)
            {
                nodes = html.QuerySelectorAll(".b-translators__list .b-translator__item")
                    .Select((translationItem, index) =>
                    {
                        var title = GetTitleFromTranslationItem(translationItem);

                        var itemId = translationItem.GetAttribute("data-id") ?? itemInfoId;
                        var translatorId = translationItem.GetAttribute("data-translator_id")?.ToIntOrNull() ?? index;
                        var videosStr = translationItem.GetAttribute("data-cdn_url") ?? string.Empty;
                        if (string.IsNullOrEmpty(itemId))
                        {
                            return null;
                        }

                        var file = new File(Site, $"rzk{itemId}_{translatorId}")
                        {
                            Title = title,
                            ItemTitle = itemTitle,
                            FrameLink = link
                        };
                        file.SetVideosFactory((f, ct) => GetVideosAsync(f, videosStr, itemId, translatorId.ToString(), null, null, ct));

                        return file;
                    })
                    .Where(file => file != null)
                    .Cast<ITreeNode>()
                    .ToList();
            }
            else
            {
                nodes = ParseTranslatesFromPage(html, itemInfoId, itemTitle).ToList();
                if (nodes.Count == 0)
                {
                    var (itemId, translatorId, _, _) = GetCDNMovieEvents(html);
                    if (translatorId != null)
                    {
                        nodes = ParseSeasonsFromPage(html, translatorId, itemId ?? itemInfoId, itemTitle).ToList();
                    }
                }
                else if (nodes.Count == 1
                    && nodes.First() is Folder folder)
                {
                    nodes = (await GetFolderChildrenAsync(folder, cancellationToken).ConfigureAwait(false)).ToList();
                }
            }

            if (nodes.Count > 0)
            {
                return nodes;
            }
            else
            {
                var (itemId, translatorId, translator, videoStr) = GetCDNMovieEvents(html);
                if (videoStr != null)
                {
                    var file = new File(Site, $"rzk{itemId ?? itemInfoId}_{translatorId}")
                    {
                        Title = translator,
                        ItemTitle = itemTitle
                    };
                    var videos = RezkaParseVideosFromPlayerJsString(videoStr, domain).ToArray();
                    file.SetVideos(videos);

                    return new[] { file }
                        .Where(file => file.Videos.Count > 0);
                }

                return Enumerable.Empty<ITreeNode>();
            }
        }

        private async Task<IEnumerable<Video>> GetVideosAsync(File file, string videosStrFallback, string itemId, string translatorId, int? season, int? episode, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var responseJson = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "ajax/get_cdn_series/"))
                .WithHeader("Referer", (file.FrameLink ?? domain).ToString())
                .WithHeader("Origin", domain.GetOrigin())
                .WithBody(new Dictionary<string, string>
                {
                    ["id"] = itemId,
                    ["translator_id"] = translatorId,
                    ["season"] = season.ToString(),
                    ["episode"] = episode.ToString(),
                    ["action"] = episode.HasValue ? "get_stream" : "get_movie"
                })
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            if (responseJson != null)
            {
                if (responseJson["subtitle"]?.ToString() is { } subtitle
                    && subtitle != "False")
                {
                    var subs = ProviderHelper.ParsePlayerJsKeyValuePairs(subtitle);
                    var langCodePerTitle = responseJson["subtitle_lns"] as JObject;

                    file.SubtitleTracks.AddRange(subs
                        .Select(tuple => (
                            title: tuple.key,
                            langCode: langCodePerTitle?[tuple.key]?.ToString(),
                            link: tuple.value?.ToUriOrNull()
                        ))
                        .Where(tuple => tuple.link != null && tuple.link.IsAbsoluteUri)
                        .Select(tuple => new SubtitleTrack(tuple.langCode, tuple.link!)
                        {
                            Title = tuple.title
                        })
                        .ToArray());
                }

                if (responseJson["url"]?.ToString() is string urlStr)
                {
                    return RezkaParseVideosFromPlayerJsString(urlStr, domain).ToArray();
                }
            }

            return RezkaParseVideosFromPlayerJsString(videosStrFallback, domain).ToArray();
        }

        private static (string? itemId, string? translatorId, string? translator, string? videoStr) GetCDNMovieEvents(IHtmlDocument html)
        {
            foreach (var script in html.Scripts.Reverse())
            {
                var match1 = Regex.Match(script.Text, @"(?:(?:initCDNMoviesEvents)|(?:initCDNSeriesEvents))\((?<itemId>\d*?),\s*(?<translatorId>\d*?),");
                var match2 = Regex.Match(script.Text, @"""streams""\s*:\s*""(?<videosStr>.*?)""");

                var itemId = match1.Groups["itemId"].Value.NotEmptyOrNull();
                var translatorId = match1.Groups["translatorId"].Value.NotEmptyOrNull();
                var translator = html.QuerySelector(".b-post__info td h2:contains('В переводе')")?
                    .ParentElement?.NextElementSibling?.TextContent.Trim().SplitLazy(2, ',').First();
                var videoStr = match2.Groups["videosStr"].Value.NotEmptyOrNull();
                if (itemId == null && translatorId == null)
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(translator) && translatorId == "110")
                {
                    translator = "Оригинальная озвучка";
                }

                return (itemId, translatorId, translator, videoStr);
            }

            return default;
        }

        private async Task<IEnumerable<ITreeNode>> GetSeasonNodesAsync(string itemId, string translatorId, string? itemTitle, CancellationToken token)
        {
            var domain = await siteProvider.GetMirrorAsync(token).ConfigureAwait(false);
            var translationDetails = await siteProvider.HttpClient
                .PostBuilder(new Uri(domain, "ajax/get_cdn_series/"))
                .WithBody(new Dictionary<string, string>
                {
                    ["id"] = itemId,
                    ["translator_id"] = translatorId,
                    ["action"] = "get_episodes"
                })
                .WithAjax()
                .SendAsync(token)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            if (translationDetails?["episodes"]?.ToString() is not string episodesHtmlStr)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var episodesHtml = WebHelper.ParseHtml(episodesHtmlStr);
            if (episodesHtml == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            return ParseSeasonsFromPage(episodesHtml, translatorId, itemId, itemTitle);
        }

        private IEnumerable<ITreeNode> ParseTranslatesFromPage(IHtmlDocument html, string itemInfoId, string? itemTitle)
        {
            return html.QuerySelectorAll(".b-translators__list .b-translator__item")
                .Select((translationItem, index) =>
                {
                    var translatorId = translationItem.GetAttribute("data-translator_id")?.ToString();
                    if (translatorId == null)
                    {
                        return null;
                    }

                    var title = GetTitleFromTranslationItem(translationItem);

                    var folder = new RezkaFolder(Site, $"rzk{itemInfoId}_{translatorId}", translatorId, FolderType.Translate, PositionBehavior.Average)
                    {
                        Title = title,
                        ItemTitle = itemTitle
                    };

                    return folder;
                })
                .Where(folder => folder?.TranslatorId != null)!;
        }

        private static string? GetTitleFromTranslationItem(IElement translationItem)
        {
            var title = translationItem.GetAttribute("title")?.ToString().Trim();
            if (translationItem.QuerySelector("img[src*='ru.png']") != null)
            {
                title += " (RU)";
            }
            else if (translationItem.QuerySelector("img[src*='ua.png']") != null)
            {
                title += " (UA)";
            }
            return title;
        }

        private IEnumerable<ITreeNode> ParseSeasonsFromPage(IHtmlDocument episodesHtml, string translatorId, string itemId, string? itemTitle)
        {
            return episodesHtml.QuerySelectorAll(".b-simple_episodes__list .b-simple_episode__item")
               .Select((episodeItem, index) =>
               {
                   var episodeItemId = episodeItem.GetAttribute("data-id") ?? itemId;
                   var season = episodeItem.GetAttribute("data-season_id")?.ToIntOrNull() ?? 1;
                   var episode = episodeItem.GetAttribute("data-episode_id")?.ToIntOrNull();

                   Debug.Assert(episode.HasValue);

                   episode ??= index;

                   var file = new File(Site, $"rzk{itemId}_{translatorId}_{season}_{episode}")
                   {
                       Season = season,
                       Episode = episode.ToRange(),
                       ItemTitle = itemTitle
                   };

                   var videosStr = episodeItem.GetAttribute("data-cdn_url") ?? string.Empty;
                   file.SetVideosFactory((f, ct) => GetVideosAsync(f, videosStr, episodeItemId, translatorId, season, episode, ct));

                   return (itemId, translatorId, season, file);
               })
               .GroupBy(tuple => (tuple.itemId, tuple.translatorId, tuple.season))
               .Select(group =>
               {
                   var seasonFolder = new Folder(Site, $"rzk{group.Key.itemId}_{group.Key.translatorId}_{group.Key.season}", FolderType.Season, PositionBehavior.Average)
                   {
                       Season = group.Key.season,
                       Title = "Сезон " + group.Key.season
                   };
                   seasonFolder.AddRange(group.Select(t => t.file));
                   return (ITreeNode)seasonFolder;
               });
        }

        private static IEnumerable<Video> RezkaParseVideosFromPlayerJsString(string videosStr, Uri domain)
        {
            return ProviderHelper.ParseVideosFromPlayerJsString(videosStr.Replace("\\/", "/"), domain)
                .Select(t => t.video)
                .Select(v =>
                {
                    v.Quality = v.Quality.Title switch
                    {
                        "1080p Ultra" => 1080,
                        "1080p" => 720,
                        "720p" => 480,
                        "480p" => 360,
                        "360p" => 240,
                        _ => v.Quality
                    };
                    return v;
                });
        }

        private class RezkaFolder : Folder
        {
            public RezkaFolder(
                Site site, string id, string translatorId, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                TranslatorId = translatorId;
            }

            public string TranslatorId { get; }

            public string? ItemTitle { get; set; }
        }
    }
}

namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    using Nito.AsyncEx;

    public class VideoCDNFileProvider : IFileProvider
    {
        private readonly VideoCDNSiteProvider siteProvider;
        private readonly PlayerJsParserService playerJsParserService;
        private readonly AsyncLazy<Dictionary<int, string>> lazyTranslates;

        public VideoCDNFileProvider(
            VideoCDNSiteProvider siteProvider,
            PlayerJsParserService playerJsParserService)
        {
            this.siteProvider = siteProvider;
            this.playerJsParserService = playerJsParserService;
            lazyTranslates = new AsyncLazy<Dictionary<int, string>>(GetTranslatorsAsync);
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        private ItemInfo? currentItem;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItem = items
                .FirstOrDefault();
        }

        public Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            if (folder == null)
            {
                return Task.FromException<IEnumerable<ITreeNode>>(new ArgumentNullException(nameof(folder)));
            }

            if (folder.Count == 0 && currentItem?.Link != null && currentItem.SiteId != null)
            {
                return GetNodesAsync(currentItem.Link, currentItem.SiteId, currentItem.Title, token);
            }

            return Task.FromResult(Enumerable.Empty<ITreeNode>());
        }

        private async Task<IEnumerable<ITreeNode>> GetNodesAsync(
            Uri iframeLink, string itemId, string? itemTitle, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var page = await siteProvider.HttpClient
                .GetBuilder(new Uri(domain, iframeLink))
                .WithHeader("Referer", iframeLink.ToString())
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            var jsonStr = page?.QuerySelector("input[id=files]")?.GetAttribute("value");
            var json = JsonHelper.ParseOrNull<JObject>(jsonStr);
            if (page == null || json == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }
            var translates = page.QuerySelectorAll(".translations > select > option[value]")
                .Select(option => (
                    value: option.GetAttribute("value")?.ToIntOrNull(),
                    title: option.TextContent.NotEmptyOrNull()?.Trim() ?? "Неизвестный перевод"))
                .Where(tuple => tuple.value.HasValue && tuple.value != 0)
                .DistinctBy(tuple => tuple.value!.Value)
                .ToDictionary(tuple => tuple.value!.Value, tuple => tuple.title);

            if (translates.Count == 0)
            {
                translates = await lazyTranslates.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return json.OfType<JProperty>()
                .Select(prop => (
                    translateId: prop.Name.ToIntOrNull() is int translateId ? translateId : (translateId = 0),
                    hasTranslate: translates.TryGetValue(translateId, out var translate),
                    translate: translate,
                    value: prop.Value))
                .Where(tuple => tuple.hasTranslate)
                .SelectMany(tuple =>
                {
                    var value = tuple.value;
                    if (tuple.value.Type == JTokenType.String)
                    {
                        var input = tuple.value.ToString();
                        var decoded = playerJsParserService.DecodeAsync(tuple.value.ToString(), siteProvider.PlayerJsConfig, cancellationToken).AsTask().GetAwaiter().GetResult()
                            ?? input;
                        value = JsonHelper.ParseOrNull<JToken>(decoded) ?? JToken.FromObject(decoded);
                    }

                    if (value.Type == JTokenType.String)
                    {
                        var videos = ParseVideosString(value.ToString(), domain).ToArray();
                        if (videos.Length == 0)
                        {
                            return Enumerable.Empty<(int translateId, string translate, int season, File file)>();
                        }

                        var file = new File(Site, $"{itemId}_0_{tuple.translateId}")
                        {
                            Title = tuple.translate,
                            ItemTitle = itemTitle,
                            FrameLink = iframeLink
                        };
                        file.SetVideos(videos);
                        return new[]
                        {
                            (tuple.translateId, tuple.translate, season: 0, file)
                        };
                    }
                    else if (value.Type == JTokenType.Array)
                    {
                        return ((JArray)value)
                            .OfType<JObject>()
                            .SelectMany((seasonOrEpisodeObject, index) =>
                            {
                                var seasonNumber = seasonOrEpisodeObject["id"]?.ToIntOrNull() ?? 1;
                                var episodes = seasonOrEpisodeObject["folder"] as JArray ?? new JArray();
                                var videoStrIfEpisode = seasonOrEpisodeObject["file"]?.ToString();

                                if (videoStrIfEpisode != null)
                                {
                                    var tuple = ParseFile(seasonOrEpisodeObject, index);
                                    if (tuple.file == null)
                                    {
                                        return Enumerable.Empty<(int translateId, string translate, int season, File file)>();
                                    }
                                    return new[] { tuple };
                                }
                                else
                                {
                                    return episodes.OfType<JObject>().Select(ParseFile).Where(t => t.file != null);
                                }

                                (int translateId, string translate, int season, File file) ParseFile(JObject episodeObject, int index)
                                {
                                    var videosStr = episodeObject["file"]?.ToString();
                                    var id = episodeObject["id"]?.ToString();
                                    var episodeNumber = id?.Split('_').Last().ToIntOrNull() ?? index;
                                    if (string.IsNullOrWhiteSpace(videosStr) || id == null)
                                    {
                                        return default;
                                    }

                                    var file = new File(Site, $"{itemId}_{seasonNumber}_{tuple.translateId}_{id}")
                                    {
                                        Season = seasonNumber,
                                        Episode = episodeNumber.ToRange(),
                                        ItemTitle = itemTitle,
                                        FrameLink = iframeLink
                                    };

                                    file.SetVideos(ParseVideosString(videosStr!, domain).ToArray());
                                    return (tuple.translateId, tuple.translate, seasonNumber, file)!;
                                }
                            });
                    }
                    return Enumerable.Empty<(int translateId, string translate, int season, File file)>();
                })
                .Where(tuple => tuple.file?.Id != null)
                .GroupBy(tuple => tuple.season)
                .OrderBy(group => group.Key)
                .SelectMany(group =>
                {
                    if (group.Key == 0)
                    {
                        return group.Select(t => t.file);
                    }
                    else
                    {
                        var seasonFolder = new Folder(Site, $"{itemId}_{group.Key}", FolderType.Season, PositionBehavior.Max)
                        {
                            Title = "Сезон " + group.Key,
                            Season = group.Key
                        };
                        seasonFolder.AddRange(group.GroupBy(t => (t.translateId, t.translate))
                            .Select(gt =>
                            {
                                var translateFolder = new Folder(Site,
                                    $"{itemId}_{group.Key}_{gt.Key.translateId}", FolderType.Translate, PositionBehavior.Average)
                                {
                                    Title = gt.Key.translate,
                                    Season = group.Key
                                };
                                translateFolder.AddRange(gt.Select(t => t.file));
                                return translateFolder;
                            }));
                        return new[] { (ITreeNode)seasonFolder }.AsEnumerable();
                    }
                })!;
        }

        private IEnumerable<Video> ParseVideosString(string videosStr, Uri domain)
        {
            videosStr = playerJsParserService.DecodeAsync(videosStr, siteProvider.PlayerJsConfig, default).AsTask().GetAwaiter().GetResult()
                ?? videosStr;
            var videos = ProviderHelper.ParseVideosFromPlayerJsString(videosStr, domain).Select(t => t.video).ToList();

            // Hack for some videos
            if (videosStr.Contains("720p")
                && videos.Find(v => v.Quality == 720) == null
                && videos.Find(v => v.Quality == 480)?.Links.First() is Uri link480
                && link480.AbsoluteUri is var link480Str
                && link480Str.Contains("480.mp4"))
            {
                var link720 = new Uri(link480.AbsoluteUri.Replace("480.mp4", "720.mp4"));
                videos.Add(new Video(link720)
                {
                    Quality = 720
                });
            }

            return videos;
        }

        private async Task<Dictionary<int, string>> GetTranslatorsAsync()
        {
            var domain = await siteProvider.GetMirrorAsync(CancellationToken.None)
                .ConfigureAwait(false);

            var json = await siteProvider.HttpClient
                .GetBuilder(new Uri(domain, "/api/translations"))
                .WithArgument("api_token", Secrets.VideoCDNApiKey)
                .SendAsync(CancellationToken.None)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            var data = json?["data"] as JArray;
            if (data == null)
            {
                return new Dictionary<int, string>();
            }

            return data
                .Select(translatorObject => (
                    translatorId: translatorObject["id"]?.ToIntOrNull() ?? 0,
                    translator: (translatorObject["smart_title"] ?? translatorObject["title"])?.ToString()))
                .Where(tuple => tuple.translatorId != 0 && tuple.translator != null)
                .DistinctBy(tuple => tuple.translatorId)
                .ToDictionary(tuple => tuple.translatorId, tuple => tuple.translator!);
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}

namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class KinoPubFileProvider : IFileProvider
    {
        private readonly KinoPubSiteProvider siteProvider;
        private readonly IPlayerParseManager playerParseManager;

        public KinoPubFileProvider(
            KinoPubSiteProvider siteProvider,
            IPlayerParseManager playerParseManager)
        {
            this.siteProvider = siteProvider;
            this.playerParseManager = playerParseManager;
        }

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => true;

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.ProForAny;

        public bool AllowDemoVideo { get; set; }

        private IEnumerable<ItemInfo>? rootItems;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            rootItems = items;
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            IEnumerable<ITreeNode> items;

            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (folder is KinoPubFolder kinoPubFolder)
            {
                items = await LoadSeasonsAsync(
                    kinoPubFolder.KinoPubId,
                    kinoPubFolder.Title,
                    kinoPubFolder.Id,
                    token).ConfigureAwait(false);
                return items;
            }
            else if (folder.Count == 0 && rootItems != null)
            {
                var rootNodes = GetRootNodes(rootItems);
                if (rootNodes.Length == 1
                    && rootNodes[0] is KinoPubFolder f)
                {
                    return await GetFolderChildrenAsync(f, token).ConfigureAwait(false);
                }
                else
                {
                    return rootNodes;
                }
            }

            return Enumerable.Empty<ITreeNode>();
        }

        private ITreeNode[] GetRootNodes(IEnumerable<ItemInfo> kinoPubItems)
        {
            return kinoPubItems
                .Where(item => !string.IsNullOrEmpty(item.SiteId))
                .Select(item =>
                {
                    var folderTitle = item.Title + (string.IsNullOrWhiteSpace(item.Details.TitleOrigin) ? "" : " / " + item.Details.TitleOrigin);

                    return new KinoPubFolder(Site, "kpub" + item.SiteId, item.SiteId!, FolderType.Item, PositionBehavior.Average)
                    {
                        Title = folderTitle,
                        Details = item.Section.Title,
                    };
                })
                .ToArray();
        }

        private async Task<IEnumerable<ITreeNode>> LoadSeasonsAsync(string kinoPubId, string? itemTitle, string parentId, CancellationToken token)
        {
            var response = await siteProvider
                .GetAsync(
                    "items/" + kinoPubId,
                    new Dictionary<string, string>
                    {
                        ["nolinks"] = "1"
                    },
                    token)
                .ConfigureAwait(false);

            if (response?["item"] is not JObject item)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var domain = await siteProvider.GetMirrorAsync(token).ConfigureAwait(false);

            if (item["seasons"] is JArray seasons)
            {
                return seasons
                    .Select(s =>
                    {
                        var title = s["title"]?.ToString();
                        var season = s["number"]?.ToIntOrNull();
                        var id = $"{parentId}_{season}";
                        var episodes = (s["episodes"] as JArray)?
                            .OfType<JObject>()
                            .Select((ep, index) =>
                            {
                                var episode = index + 1;
                                var file = ParseFile(ep, $"{id}_{episode}", domain);
                                if (file == null)
                                {
                                    return null;
                                }
                                file.Episode = episode.ToRange();
                                file.Season = season;
                                file.ItemTitle = itemTitle;
                                file.FrameLink = new Uri(domain, $"item/view/{kinoPubId}/s{season}e{episode}");
                                return file;
                            })
                            .Where(file => file != null)!
                            ?? Enumerable.Empty<File>();

                        var folder = new Folder(Site, id, FolderType.Season, PositionBehavior.Average)
                        {
                            Title = season.HasValue ? $"Сезон {season} {title}" : title,
                            Season = season
                        };
                        folder.AddRange(episodes!);

                        return folder;
                    })
                    .Where(folder => folder.Count > 0);
            }

            if (item["videos"] is JArray videos)
            {
                return videos
                    .OfType<JObject>()
                    .Select((v, index) =>
                    {
                        var file = ParseFile(v, videos.Count == 1 ? parentId : $"{parentId}_{index}", domain);
                        if (file == null)
                        {
                            return null;
                        }
                        if (string.IsNullOrEmpty(file.Title))
                        {
                            file.Title = itemTitle;
                        }

                        file.ItemTitle = itemTitle;
                        file.FrameLink = new Uri(domain, $"item/view/{kinoPubId}");
                        return file;
                    })
                    .Where(file => file != null)!;
            }

            return Enumerable.Empty<ITreeNode>();
        }

        private File? ParseFile(JObject jFile, string id, Uri baseDomain)
        {
            if (jFile["id"]?.ToIntOrNull() is not int mid)
            {
                return null;
            }

            var file = new File(Site, id)
            {
                Title = jFile["title"]?.ToString(),
                PlaceholderImage = jFile["thumbnail"]?.ToUriOrNull()
            };

            file.SetVideosFactory(async (file, ct) =>
            {
                var response = await siteProvider
                    .GetAsync(
                        "/v1/items/media-links",
                        new Dictionary<string, string>
                        {
                            ["mid"] = mid.ToString()
                        },
                        ct)
                    .ConfigureAwait(false);
                if (response == null)
                {
                    return Enumerable.Empty<Video>();
                }

                file.SubtitleTracks.AddRange((response["subtitles"] as JArray ?? jFile["subtitles"] as JArray ?? new JArray())
                   .Select(token => ParseSubtitle(token, baseDomain))
                   .Where(sub => sub?.Link != null)
                   .ToArray()!);
                file.EmbededAudioTracks.AddRange((response["audios"] as JArray ?? jFile["audios"] as JArray ?? new JArray())
                    .Select(token => ParseAudioTrack(token))
                    .Where(sub => sub != null)
                    .ToArray()!);

                //var deviceInfo = await siteProvider.GetDeviceInfoAsync(ct).ConfigureAwait(false);
                //var streamingType = (deviceInfo?["device"]?["settings"]?["streamingType"]?["value"] as JArray)?
                //    .FirstOrDefault(v => v["selected"]?.ToIntOrNull() == 1)?["label"]?.ToString()?.ToLowerInvariant() ?? "unknown";

                return (response["files"] as JArray ?? jFile["files"] as JArray ?? new JArray())
                    .Select(v => (
                        quality: v["quality"]?.ToString(),
                        url: (v["url"]?["http"] ?? v["url"]?["hls"] ?? v["url"]?["hls4"])?.ToUriOrNull(baseDomain)))
                    .Where(t => t.url != null && (AllowDemoVideo || !t.url.GetPath().Contains("/demo")))
                    .Select(t => new Video(t.url!) { Quality = t.quality ?? default(Quality) })
                    .ToArray();
            });

            return file;
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (itemInfo == null)
            {
                return null;
            }

            var response = await siteProvider
                .GetAsync(
                    "items/trailer",
                    new Dictionary<string, string>
                    {
                        ["id"] = itemInfo.SiteId!
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            var trailerUrl = (response?["trailer"] as JObject)?["url"]?.ToUriOrNull();
            if (response == null || trailerUrl == null
                || !playerParseManager.CanOpenFromLinkOrHostingName(trailerUrl, Site.Any))
            {
                return null;
            }
            var file = await playerParseManager.ParseFromUriAsync(trailerUrl, Site.Any, cancellationToken).ConfigureAwait(false);
            if (file == null)
            {
                return null;
            }
            file.Title ??= itemInfo.Title + (string.IsNullOrWhiteSpace(itemInfo.Details.TitleOrigin) ? "" : " / " + itemInfo.Details.TitleOrigin);
            file.IsTrailer = true;
            file.ItemTitle = itemInfo.Title;
            file.PlaceholderImage ??= response["trailer"]?["thumb"]?.ToUriOrNull(itemInfo.Link);

            var folder = new Folder(Site, $"kp_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
            folder.Add(file);
            return folder;
        }

        private static SubtitleTrack? ParseSubtitle(JToken token, Uri baseDomain)
        {
            if (token["url"]?.ToUriOrNull(baseDomain) is not Uri subLink)
            {
                return null;
            }
            return new SubtitleTrack(token["lang"]?.ToString().ToUpper(), subLink)
            {
                Offset = TimeSpan.FromSeconds(token["shift"]?.ToIntOrNull() ?? 0)
            };
        }

        private static AudioTrack? ParseAudioTrack(JToken token)
        {
            var index = token["index"]?.ToIntOrNull() - 1;
            var author = (token["author"] as JObject)?["title"]?.ToString().NotEmptyOrNull();
            var lang = token["lang"]?.ToString().NotEmptyOrNull();
            var title = (token["type"] as JObject)?["title"]?.ToString()?.NotEmptyOrNull();

            if (lang == null || index == null)
            {
                return null;
            }

            var details = lang.ToUpperInvariant();
            if (author != null)
            {
                details += $", {author}";
            }

            if (title == null)
            {
                title = details;
            }
            else
            {
                title += $" ({details})";
            }

            return new AudioTrack(lang)
            {
                Title = title,
                Index = index.Value
            };
        }

        public class KinoPubFolder : Folder
        {
            public KinoPubFolder(Site site, string id, string kinoPubId, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                KinoPubId = kinoPubId;
            }

            public string KinoPubId { get; }
        }
    }
}

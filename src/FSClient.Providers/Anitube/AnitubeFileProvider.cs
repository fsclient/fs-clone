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
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class AnitubeFileProvider : IFileProvider
    {
        private readonly AnitubeSiteProvider siteProvider;
        private readonly IPlayerParseManager playerParseManager;

        private IEnumerable<ItemInfo>? currentItems;

        public AnitubeFileProvider(
            AnitubeSiteProvider siteProvider,
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
            currentItems = items;
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            if (folder.Count > 0)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            if (folder is AnitubeFolder anitubeFolder)
            {
                var items = (await GetTranslatesAsync(anitubeFolder.Link, anitubeFolder.Id, anitubeFolder.Title, token)
                    .ConfigureAwait(false))
                    .ToArray();

                if (items.Length == 1
                    && items[0] is Folder translateFolder)
                {
                    items = translateFolder.ItemsSource.ToArray();
                }

                return items;
            }
            else if (currentItems != null)
            {
                var rootItems = GetItemFolders(currentItems).ToArray();
                if (rootItems.Length == 1
                    && rootItems[0] is Folder innerFolder)
                {
                    return await GetFolderChildrenAsync(innerFolder, token).ConfigureAwait(false);
                }

                return rootItems;
            }

            return Enumerable.Empty<ITreeNode>();
        }

        private async Task<IEnumerable<ITreeNode>> GetTranslatesAsync(Uri link, string id, string? title, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var text = await siteProvider.HttpClient
                .GetBuilder(new Uri(domain, link))
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);
            var match = Regex.Match(text ?? string.Empty, @"RalodePlayer\.init\((?<args>.+?)\);");
            var jsonArguments = JsonHelper.ParseOrNull<JArray>($"[{match.Groups["args"].Value}]");
            if (jsonArguments == null
                || jsonArguments.Count < 2
                || jsonArguments[0] is not JArray translateNamesArray
                || jsonArguments[1] is not JArray filesArray)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            return translateNamesArray
                .Select((translateNameToken, index) => (
                    translateName: translateNameToken.ToString(),
                    episodesArray: filesArray[index]))
                .Where(tuple => tuple.episodesArray != null)
                .Select(tuple =>
                {
                    var translateFolder = new Folder(Site, $"{id}_{tuple.translateName.GetDeterministicHashCode()}", FolderType.Translate, PositionBehavior.Average)
                    {
                        Title = tuple.translateName
                    };
                    translateFolder.AddRange(tuple.episodesArray
                        .Select((episodeNode, index) =>
                        {
                            var iframeHtmlCode = episodeNode["code"]?.ToString();
                            if (iframeHtmlCode == null)
                            {
                                return null;
                            }
                            var iframeSrc = WebHelper.ParseHtml(iframeHtmlCode)?.QuerySelector("iframe")?.GetAttribute("src").ToUriOrNull();
                            if (iframeSrc == null
                                || !playerParseManager.CanOpenFromLinkOrHostingName(iframeSrc, Site.Any))
                            {
                                return null;
                            }

                            var episodeNumber = Regex.Match(episodeNode["name"]?.ToString() ?? string.Empty, @"(<epNumber>\d+)\sсерія").Groups["epNumber"]?.Value?.ToIntOrNull() ?? (index + 1);
                            var episodeId = episodeNode["sid"]?.ToIntOrNull() ?? episodeNumber;
                            var file = new File(Site, $"{translateFolder.Id}_{episodeId}")
                            {
                                Episode = episodeNumber.ToRange(),
                                ItemTitle = title,
                                FrameLink = link
                            };
                            file.SetVideosFactory(async (file, ct) =>
                            {
                                var playerFile = await playerParseManager.ParseFromUriAsync(iframeSrc, Site.Any, ct).ConfigureAwait(false);
                                if (playerFile == null)
                                {
                                    return Enumerable.Empty<Video>();
                                }
                                if (playerFile.SubtitleTracks.Any())
                                {
                                    file.SubtitleTracks.AddRange(playerFile.SubtitleTracks);
                                }
                                return playerFile.Videos;
                            });

                            return file;
                        })
                        .Where(file => file != null)!);
                    return translateFolder;
                })
                .Where(folder => folder.Count > 0);
        }

        private IEnumerable<ITreeNode> GetItemFolders(IEnumerable<ItemInfo> currentItems)
        {
            return currentItems
                .Where(item => item.Link != null)
                .Select(item => new AnitubeFolder(Site, $"anitb{item.SiteId}", item.Link!, FolderType.Item, PositionBehavior.Max)
                {
                    Title = item.Title
                });
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var html = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, itemInfo.Link!))
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            var youtubeLink = html?.QuerySelector(".story_c > .rcol a[href*=youtu]")?.GetAttribute("href")?.ToUriOrNull();
            if (youtubeLink == null
                || !playerParseManager.CanOpenFromLinkOrHostingName(youtubeLink, Sites.Youtube))
            {
                return null;
            }

            var file = await playerParseManager.ParseFromUriAsync(youtubeLink, Sites.Youtube, cancellationToken).ConfigureAwait(false);
            if (file != null)
            {
                file.IsTrailer = true;
            }

            var folder = new Folder(Site, $"ant_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
            folder.Add(file!);
            return folder;
        }

        public class AnitubeFolder : Folder
        {
            public AnitubeFolder(Site site, string id, Uri link, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                Link = link;
            }

            public Uri Link { get; set; }
        }
    }
}

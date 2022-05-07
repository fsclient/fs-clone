namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class NekomoriFileProvider : IFileProvider
    {
        private ItemInfo? initedItem;

        private readonly NekomoriSiteProvider siteProvider;
        private readonly IPlayerParseManager playerParseManager;

        public NekomoriFileProvider(
            NekomoriSiteProvider siteProvider,
            IPlayerParseManager playerParseManager)
        {
            this.siteProvider = siteProvider;
            this.playerParseManager = playerParseManager;
        }

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => false;

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            initedItem = items
                .FirstOrDefault();
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (initedItem == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            if (folder.Count == 0
                && folder is NekomoriFolder nkFolder)
            {
                var episode = folder.Episode?.Start.Value ?? 1;
                var episodesCount = nkFolder.Parent?.ItemsSource.LastOrDefault()?.Episode?.Start.Value ?? 1;
                var files = await LoadTranslatesAsync(nkFolder.NekomoriId, nkFolder.Link, episode, episodesCount, nkFolder.ItemTitle, token)
                    .ConfigureAwait(false);

                return files;
            }
            else if (folder.Count == 0 && !string.IsNullOrEmpty(initedItem.SiteId) && initedItem.Link is not null)
            {
                return GetRootFolders(initedItem!);
            }

            return Enumerable.Empty<ITreeNode>();
        }

        private IEnumerable<NekomoriFolder> GetRootFolders(ItemInfo item)
        {
            var rootFolderId = "nk" + item.SiteId;
            var itemId = item.SiteId.ToIntOrNull()!.Value;

            if (!item.Section.Modifier.HasFlag(SectionModifiers.Serial)
                && (item.Details.Status.CurrentEpisode ?? 0) <= 1
                && (item.Details.Status.TotalEpisodes ?? 0) <= 1)
            {
                var rootFolder = new NekomoriFolder(Site, item.Link!, rootFolderId, itemId, FolderType.Item, PositionBehavior.Average)
                {
                    Title = item.Title,
                    ItemTitle = item.Title
                };
                yield return rootFolder;
            }
            else
            {
                var episodesCount = item.Details.Status.CurrentEpisode ?? item.Details.Status.TotalEpisodes ?? 0;
                var rootFolders = Enumerable.Range(1, episodesCount)
                    .Select(episodeNumber => new NekomoriFolder(Site, item.Link!,
                        rootFolderId + "_" + episodeNumber, itemId, FolderType.Episode,
                        PositionBehavior.Max)
                    {
                        Title = "Серия " + episodeNumber,
                        ItemTitle = item.Title,
                        Episode = episodeNumber.ToRange()
                    });

                foreach (var rootFolder in rootFolders)
                {
                    yield return rootFolder;
                }
            }
        }

        private async Task<IEnumerable<File>> LoadTranslatesAsync(
            int nekomoriId, Uri nekomoriWakanimLink, int episodeNumber, int totalEpisodes, string? itemTitle, CancellationToken cancellationToken)
        {
            var seed = nekomoriWakanimLink.Segments.LastOrDefault()?.Trim('/')
                ?? throw new InvalidOperationException("Invalid nekomori link");

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var translates = await siteProvider.HttpClient
                .GetBuilder(new Uri(nekomoriWakanimLink, $"/cdn/list"))
                .WithArgument("page", episodeNumber.ToString())
                .WithHeader("Referer", nekomoriWakanimLink.ToString())
                .WithHeader("Origin", nekomoriWakanimLink.GetOrigin())
                .WithHeader("seed", seed)
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsJson(cancellationToken)
                .ConfigureAwait(false) ?? new JsonElement();

            return translates
                .ToItems()
                .Select(translateObject => (
                    id: translateObject.ToPropertyOrNull("id")?.ToStringOrNull(),
                    link: translateObject.ToPropertyOrNull("src")?.ToUriOrNull(),
                    kind: GetFriendlyTranslationTypeTitle(translateObject.ToPropertyOrNull("kind")?.ToStringOrNull()),
                    language: GetFriendlyLanguage(translateObject.ToPropertyOrNull("lang")?.ToStringOrNull()),
                    bluray: translateObject.ToPropertyOrNull("bluray")?.ToBoolOrNull() ?? false,
                    author: translateObject.ToPropertyOrNull("authors")?.ToItems().FirstOrDefault().ToPropertyOrNull("name")?.ToStringOrNull(),
                    player: Site.Parse(translateObject.ToPropertyOrNull("player")?.ToPropertyOrNull("name")?.ToStringOrNull(), Site.Any, true)
                ))
                .Where(tuple => tuple.id != null && tuple.link != null
                    && playerParseManager.CanOpenFromLinkOrHostingName(tuple.link, tuple.player))
                .OrderByDescending(tuple => tuple.bluray)
                .Select(tuple =>
                {
                    var file = GenerateFile(    
                        $"nk{nekomoriId}_{episodeNumber}_{tuple.id}",
                         $"{(tuple.bluray ? "BD " : "")}({tuple.player}) {tuple.author}",
                         tuple.kind,
                        itemTitle,
                        episodeNumber);
                    file.FrameLink = tuple.link;
                    file.Language = tuple.language;
                    file.Author = tuple.author;
                    file.Host = tuple.player;
                    return file;
                });

            NekomoriFile GenerateFile(
                string id, string title, string? group,
                string? itemTitle, int fileEp, NekomoriFile? dependedOnFile = null, List<File>? filePlaylist = null)
            {
                var file = new NekomoriFile(Site, id)
                {
                    Title = title,
                    Episode = fileEp.ToRange(),
                    Group = group,
                    ItemTitle = itemTitle
                };
                if (filePlaylist == null)
                {
                    filePlaylist = new List<File>();
                    filePlaylist.AddRange(Enumerable.Range(1, totalEpisodes)
                        .Select(ep => fileEp == ep
                            ? file
                            : GenerateFile($"nk{nekomoriId}_{ep}", title, group, itemTitle, ep, file, filePlaylist)));
                }
                file.Playlist = filePlaylist;

                file.SetVideosFactory(async (file, token) =>
                {
                    if (dependedOnFile != null)
                    {
                        file.ItemInfo = dependedOnFile.ItemInfo;

                        if (dependedOnFile.Parent?.Parent is Folder itemFolder)
                        {
                            file.Parent = itemFolder.ItemsSource.OfType<NekomoriFolder>()
                                .FirstOrDefault(f => f.Episode?.Start.Value == fileEp);
                        }

                        if (file.Parent is Folder nekomoriFolder)
                        {
                            var children = await GetFolderChildrenAsync(nekomoriFolder, token).ConfigureAwait(false);

                            var items = children.OfType<NekomoriFile>()
                                .OrderByDescending(el => el.Group == dependedOnFile.Group)
                                .ThenByDescending(el => el.Author == dependedOnFile.Author)
                                .ThenByDescending(el => el.Host == dependedOnFile.Host)
                                .Select((node, index) =>
                                {
                                    if (index == 0)
                                    {
                                        ((NekomoriFile)file).SetId(node.Id);
                                        file.Title = node.Title;
                                        file.FrameLink = node.FrameLink;
                                        file.Group = node.Group;
                                        file.ItemTitle = node.ItemTitle;
                                        file.ItemInfo = node.ItemInfo ?? dependedOnFile.ItemInfo;
                                        return file;
                                    }

                                    return node;
                                })
                                .ToArray();
                            nekomoriFolder.Clear();
                            nekomoriFolder.AddRange(items);
                        }
                    }
                    var playerFile = await playerParseManager
                        .ParseFromUriAsync(file.FrameLink!, ((NekomoriFile)file).Host, token)
                        .ConfigureAwait(false);
                    if (playerFile == null)
                    {
                        return Enumerable.Empty<Video>();
                    }

                    if (playerFile.SubtitleTracks.Any())
                    {
                        file.SubtitleTracks.AddRange(playerFile.SubtitleTracks
                            .Select(s => new SubtitleTrack(((NekomoriFile)file).Language ?? s.Language, s.Link) { Title = s.Title })
                            .ToArray());
                    }

                    return playerFile.Videos;
                });
                return file;
            }
        }

        private static string? GetFriendlyLanguage(string? langId)
        {
            return langId?.ToUpper();
        }

        private static string? GetFriendlyTranslationTypeTitle(string? kindId)
        {
            return kindId switch
            {
                "source" => "Оригинал",
                "subs" => "Субтитры",
                "dub" => "Озвучка",
                _ => null
            };
        }

        private class NekomoriFolder : Folder
        {
            public NekomoriFolder(Site site, Uri link, string id, int nekomoriId, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                NekomoriId = nekomoriId;
                Link = link;
            }

            public int NekomoriId { get; }

            public Uri Link { get; }

            public string? ItemTitle { get; set; }
        }

        private class NekomoriFile : File
        {
            public NekomoriFile(Site site, string id) : base(site, id)
            {
            }

            public string? Language { get; set; }

            public string? Author { get; set; }

            public Site Host { get; set; }

            public void SetId(string id)
            {
                Id = id;
            }
        }
    }
}

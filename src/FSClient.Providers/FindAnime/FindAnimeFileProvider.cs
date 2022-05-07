namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class FindAnimeFileProvider : IFileProvider
    {
        private readonly FindAnimeSiteProvider siteProvider;
        private readonly IPlayerParseManager playerParseManager;

        private ItemInfo? currentItem;

        public FindAnimeFileProvider(
            FindAnimeSiteProvider siteProvider,
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
            if (folder.Count > 0)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            if (folder is FindAnimeFolder findAnimeFolder)
            {
                var translates = await GetTranslatesAsync(findAnimeFolder.Link, findAnimeFolder.Id, token)
                    .ConfigureAwait(false);

                return translates;
            }
            else
            {
                if (currentItem?.SiteId == null)
                {
                    return Enumerable.Empty<ITreeNode>();
                }

                var rootItems = await GetEpisodesFromItemAsync(currentItem.SiteId, token).ConfigureAwait(false);

                return rootItems;
            }
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var siteId = itemInfo.SiteId;
            if (string.IsNullOrEmpty(siteId)
                || itemInfo.Section.Modifier.HasFlag(SectionModifiers.Film))
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var page = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, siteId))
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            if (page == null)
            {
                return null;
            }

            var files = GetFilesListFromPage(domain, page, $"fa{siteId}", "_t", setupPlaylist: false)
                .Select(f =>
                {
                    f.IsTrailer = true;
                    return (File)f;
                })
                .ToList();

            var folder = new Folder(Site, $"fa_t_{siteId}", FolderType.Item, PositionBehavior.Average);
            folder.AddRange(files);
            return folder;
        }

        private async Task<IEnumerable<Folder>> GetEpisodesFromItemAsync(
            string itemId, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var itemLink = new Uri(domain, $"{itemId}/");
            var page = await siteProvider
                .HttpClient
                .GetBuilder(itemLink)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            if (page == null)
            {
                return Enumerable.Empty<Folder>();
            }

            var itemTitle = page.QuerySelector("[itemprop=name][content]")?.GetAttribute("content");

            return page
                .QuerySelectorAll(".chapters-link tr td:first-child a[href*=series]")
                .Select(anchor =>
                {
                    var link = anchor.GetAttribute("href")?.ToUriOrNull(itemLink);
                    if (link == null)
                    {
                        return null;
                    }

                    var lastSegment = link.Segments.Last().Trim('/');
                    var episode = GetEpisodeNumberFromLink(link);
                    var id = $"fa{itemId}_{episode?.ToString() ?? lastSegment}";

                    var episodeName = anchor.FirstChild?.TextContent?
                        .Replace("Фильм полностью", "")
                        .Replace(itemTitle ?? "", "")
                        .Split(new[] { $"{episode} - " }, StringSplitOptions.RemoveEmptyEntries)
                        .LastOrDefault()?
                        .Trim();

                    return new FindAnimeFolder(Site, id, link, FolderType.Episode, PositionBehavior.Max)
                    {
                        Episode = episode.ToRange(),
                        Title = (episode.HasValue
                            ? $"Серия {episode} {episodeName}"
                            : episodeName ?? itemTitle)?.Trim()
                    };
                })
                .Reverse()!;
        }

        private async Task<IEnumerable<FindAnimeFile>> GetTranslatesAsync(
            Uri link, string id, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var itemLink = new Uri(domain, link);

            var page = await siteProvider
                .HttpClient
                .GetBuilder(itemLink)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .WithArgument("mtr", "1")
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            if (page == null)
            {
                return Enumerable.Empty<FindAnimeFile>();
            }

            var itemTitle = page.QuerySelector(".title a.manga-link")?.TextContent?.Trim();
            var episode = GetEpisodeNumberFromLink(itemLink);
            return GetFilesListFromPage(domain, page, id, itemTitle: itemTitle, episode: episode);
        }

        private IEnumerable<FindAnimeFile> GetFilesListFromPage(
            Uri domain,
            IHtmlDocument html,
            string baseId, string? suffixId = null,
            string? itemTitle = null, int? episode = null,
            bool setupPlaylist = true)
        {
            return html.QuerySelectorAll("table .chapter-link, table .chapter-table-link")
                .Select(element =>
                {
                    var hosting = element.QuerySelector(".open_link .text-additional")?.TextContent.Trim();
                    var hostingSite = Site.Parse(hosting?.GetLettersAndDigits().ToLowerInvariant(), Site.Any, true);
                    var iframeLink = element.QuerySelector("input.embed_source[value]")?
                        .GetAttribute("value")?
                        .Split('"', '\'')
                        .Skip(1)
                        .FirstOrDefault()?
                        .ToUriOrNull(domain);

                    if (iframeLink == null
                        || !playerParseManager.CanOpenFromLinkOrHostingName(iframeLink, hostingSite))
                    {
                        return null;
                    }

                    var translateId = element.QuerySelector(".open_link[data-sid]")?.GetAttribute("data-sid");
                    var type = element.QuerySelector(".details")?.FirstChild?.TextContent.Replace('(', ' ').Trim();
                    var translator = element.QuerySelector(".details .person-link")?.TextContent.Trim();
                    if (translator == null)
                    {
                        var textNode = element.QuerySelector(".video-info")?.ChildNodes
                            .OfType<IText>()
                            .Where(t => !string.IsNullOrWhiteSpace(t.Text))
                            .FirstOrDefault();
                        if (textNode != null)
                        {
                            translator = GetFriendlyVideoKindName(textNode.Text.Trim());
                        }
                    }

                    var file = new FindAnimeFile(Site, $"{baseId}_{translateId}{suffixId}");
                    file.FrameLink = iframeLink;
                    file.Episode = episode.ToRange();
                    file.ItemTitle = itemTitle;
                    file.Group = type;
                    file.Hosting = hosting;
                    file.Translator = translator;
                    file.Title = $"({hosting}) {translator}".Trim();

                    file.SetVideosFactory(async (file, token) =>
                    {
                        var playerFile = await playerParseManager
                            .ParseFromUriAsync(iframeLink, hostingSite, token)
                            .ConfigureAwait(false);
                        if (playerFile == null)
                        {
                            return Array.Empty<Video>();
                        }

                        file.SubtitleTracks.AddRange(playerFile.SubtitleTracks);
                        return playerFile.Videos;
                    });

                    if (setupPlaylist)
                    {
                        SetupPlaylist(file, domain, html);
                    }

                    return file;
                })
                .Where(file => file != null)!;
        }

        private void SetupPlaylist(FindAnimeFile rootFile, Uri domain, IHtmlDocument html)
        {
            var playlist = new List<File>();
            var enumerable = html
                .QuerySelectorAll(".topControl #chapterSelectorSelect option[value*='/']")
                .Reverse()
                .Select(option =>
                {
                    var link = option.GetAttribute("value")?.Replace("?mtr=1", "").ToUriOrNull(domain);
                    if (link == null)
                    {
                        return null;
                    }

                    var episode = GetEpisodeNumberFromLink(link);
                    if (episode == rootFile.Episode?.Start.Value)
                    {
                        return rootFile;
                    }

                    var itemId = link.Segments.Skip(1).FirstOrDefault()?.Trim('/');
                    var lastSegment = link.Segments.Skip(2).LastOrDefault()?.Trim('/');

                    var id = $"fa{itemId}_{episode?.ToString() ?? lastSegment}";
                    var file = new FindAnimeFile(Site, id)
                    {
                        Episode = episode.ToRange(),
                        Playlist = playlist
                    };

                    file.SetVideosFactory(async (file, token) =>
                    {
                        var parentFolder = (rootFile.Parent?.Parent as Folder)?
                            .ItemsSource.OfType<FindAnimeFolder>()
                            .FirstOrDefault(f => f.Link.GetPath() == link.GetPath());
                        file.Parent = parentFolder;

                        IEnumerable<FindAnimeFile> translates;
                        if (parentFolder?.Count > 0)
                        {
                            translates = parentFolder.ItemsSource.OfType<FindAnimeFile>().ToArray();
                        }
                        else
                        {
                            translates = await GetTranslatesAsync(link, id, token).ConfigureAwait(false);
                        }

                        var similarFile = translates
                            .OrderByDescending(translate => translate.Group == rootFile.Group)
                            .ThenByDescending(translate => translate.Translator == rootFile.Translator)
                            .ThenByDescending(translate => translate.Hosting == rootFile.Hosting)
                            .FirstOrDefault();
                        if (similarFile == null)
                        {
                            return Array.Empty<Video>();
                        }

                        if (file is FindAnimeFile faFile)
                        {
                            faFile.SetId(similarFile.Id);
                            faFile.Hosting = similarFile.Hosting;
                            faFile.Translator = similarFile.Translator;
                        }
                        file.Title = similarFile.Title;
                        file.ItemTitle = similarFile.ItemTitle;
                        file.Group = similarFile.Group;

                        if (parentFolder != null)
                        {
                            parentFolder.Clear();
                            parentFolder.AddRange(translates
                                .Select(translate => translate.Id == file.Id ? file : translate));
                        }

                        await similarFile.PreloadAsync(token).ConfigureAwait(false);

                        file.SubtitleTracks.AddRange(similarFile.SubtitleTracks);

                        return similarFile.Videos;
                    });

                    return (File)file;
                })
                .Where(file => file != null);
            playlist.AddRange(enumerable!);

            rootFile.Playlist = playlist;
        }

        private static string GetFriendlyVideoKindName(string kind)
        {
            return (kind.ToLowerInvariant()) switch
            {
                "pv" => "Promotional Video",
                "ed" => "Ending",
                "op" => "Opening",
                _ => kind,
            };
        }

        private static int? GetEpisodeNumberFromLink(Uri link)
        {
            var lastSegment = link.Segments.Last().Trim('/');
            var episode = lastSegment.Replace("series", "").ToIntOrNull();
            if (episode == 0)
            {
                episode = null;
            }
            return episode;
        }

        public class FindAnimeFolder : Folder
        {
            public FindAnimeFolder(Site site, string id, Uri link, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                Link = link;
            }

            public Uri Link { get; }
        }

        private class FindAnimeFile : File
        {
            public FindAnimeFile(Site site, string id) : base(site, id)
            {
            }

            public string? Hosting { get; set; }

            public string? Translator { get; set; }

            public void SetId(string id)
            {
                Id = id;
            }
        }
    }
}

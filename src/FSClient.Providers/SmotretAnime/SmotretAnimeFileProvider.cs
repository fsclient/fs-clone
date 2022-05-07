namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class SmotretAnimeFileProvider : IFileProvider
    {
        private ItemInfo? initedItem;

        private readonly SmotretAnimeSiteProvider saSiteProvider;
        private readonly SmotretAnimePlayerParseProvider saPlayerParseProvider;
        private readonly ShikiSiteProvider shikiSiteProvider;

        public SmotretAnimeFileProvider(
            SmotretAnimeSiteProvider saSiteProvider,
            SmotretAnimePlayerParseProvider saPlayerParseProvider,
            ShikiSiteProvider shikiSiteProvider)
        {
            this.saSiteProvider = saSiteProvider;
            this.saPlayerParseProvider = saPlayerParseProvider;
            this.shikiSiteProvider = shikiSiteProvider;
        }

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => false;

        public Site Site => saSiteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.ProForAny;

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

            if (folder is SAFolder saFolder)
            {
                var items = await LoadTranslateFilesWithTypesAsync(saFolder, token).ConfigureAwait(false);
                return items;
            }
            else if (folder.Count == 0 && !string.IsNullOrEmpty(initedItem.SiteId) && initedItem.Link != null)
            {
                var shikiDomain = await shikiSiteProvider.GetMirrorAsync(token).ConfigureAwait(false);
                var domain = await saSiteProvider.GetMirrorAsync(token).ConfigureAwait(false);
                var nameLinkPart = string.Join("-", initedItem.Link.Segments.LastOrDefault()?.Split('-').Skip(1).ToArray()
                    ?? new[] { "l" });

                var shikiLink = new Uri(shikiDomain, $"animes/{initedItem.SiteId}-{nameLinkPart}");
                var link = GenerateSAFromShikimoriLink(domain, shikiLink);
                var items = await LoadEpisodesAsync(link, token).ConfigureAwait(false);
                return items;
            }

            return Enumerable.Empty<ITreeNode>();
        }

        private async Task<IEnumerable<File>> LoadTranslateFilesWithTypesAsync(SAFolder saFolder, CancellationToken token)
        {
            var typeFolders = await LoadTranslateTypesAsync(saFolder.Link, saFolder.Id, token).ConfigureAwait(false);

            return await typeFolders
                .OfType<SAFolder>()
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((f, ct) => new ValueTask<IEnumerable<File>>(
                    LoadFilesAsync(f.Link, saFolder.Id, f.Title, saFolder.ItemTitle, saFolder.Playlist, ct)))
                .SelectMany(files => files.ToAsyncEnumerable())
                .ToListAsync(token)
                .ConfigureAwait(false);
        }

        private async Task<IEnumerable<Folder>> LoadEpisodesAsync(Uri link, CancellationToken cancellationToken)
        {
            var uwpRedirectException = false;
            var response = await saSiteProvider.HttpClient
                .GetBuilder(link)
                .Catch<HttpRequestException>(ex =>
                    uwpRedirectException = unchecked((uint)ex.HResult) == 0x80072F7C)
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
            if (uwpRedirectException
                || (response?.StatusCode == HttpStatusCode.MovedPermanently
                && response.RequestMessage!.RequestUri == response.Headers.Location))
            {
                var hentaiDomain = new UriBuilder(response?.Headers.Location ?? link);
                hentaiDomain.Host = "hentai365.ru";
                response = await saSiteProvider.HttpClient
                    .GetBuilder(hentaiDomain.Uri)
                    .SendAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            var html = await response
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            var itemTitle = (html?.QuerySelector(".line-1 a[href]")?.FirstChild ?? html?.QuerySelector(".line-1"))?
                .TextContent?
                .Replace("смотреть онлайн", "")
                .Trim();

            var episodes = html?.QuerySelectorAll(".m-episode-item[href]")
                .Select(a => (
                    href: a.GetAttribute("href")?.ToUriOrNull(response!.RequestMessage!.RequestUri!),
                    title: a.LastChild?.TextContent))
                .Select(t => (t.href, id: GetIdFromLink(t.href), t.title))
                .Where(t => t.href != null && !string.IsNullOrEmpty(t.id))
                .ToList()
                ?? new List<(Uri href, string id, string title)>()!;

            return episodes
                .Select(t => new SAFolder(Site, t.id!, t.href!, FolderType.Episode, PositionBehavior.Max)
                {
                    Title = t.title,
                    ItemTitle = itemTitle,
                    Episode = t.href!.Segments
                        .LastOrDefault()?
                        .Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault()?
                        .ToIntOrNull()?
                        .ToRange(),
                    Playlist = episodes.Select(e => e.href)!
                });

            static string? GetIdFromLink(Uri? epLink)
            {
                if (epLink == null)
                {
                    return null;
                }

                var itemId = epLink.Segments.Skip(2).FirstOrDefault()?.Split('-').Last().GetDigits();
                var epId = epLink.Segments.Skip(3).FirstOrDefault()?.Split('-').Last().GetDigits();
                return $"{itemId}_{epId}";
            }
        }

        private async Task<IEnumerable<Folder>> LoadTranslateTypesAsync(Uri link, string parentId, CancellationToken cancellationToken)
        {
            var html = await saSiteProvider.HttpClient
                .GetBuilder(link)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (html == null)
            {
                return Enumerable.Empty<Folder>();
            }

            var episode = html.QuerySelector("meta[property='ya:ovs:episode'][content]")?.GetAttribute("content")?.ToIntOrNull();

            return html.QuerySelectorAll(".m-select-translation-type a[href]")
                .Select(a => (href: a.GetAttribute("href")?.ToUriOrNull(link), title: a.TextContent))
                .Where(t => t.href != null)
                .Select(t => new SAFolder(Site, parentId, t.href!, FolderType.Translate, PositionBehavior.Max)
                {
                    Title = GetFriendlyTranslationTypeTitle(t.title),
                    Episode = episode.ToRange()
                });
        }

        private async Task<IEnumerable<File>> LoadFilesAsync(
            Uri link, string parentId, string? group, string? itemTitle,
            IEnumerable<Uri> playlist, CancellationToken cancellationToken)
        {
            var html = await saSiteProvider.HttpClient
                .GetBuilder(link)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            var currentEpId = parentId;
            parentId = parentId.Split('_').First();

            var episode = html?.QuerySelector("meta[property='ya:ovs:episode']")?.GetAttribute("content")?.ToIntOrNull();

            return html?.QuerySelectorAll(".m-select-translation-variant a[href]")
                .Select(a => (href: a.GetAttribute("href"), title: a.TextContent))
                .Select(t => (href: t.href?.ToUriOrNull(link), id: t.href?.Split('-').Last().GetDigits(), t.title))
                .Where(t => t.href != null)
                .Select(t => GenerateFile(parentId + "_" + t.id, t.href!, t.title, group, itemTitle, episode))
                ?? Enumerable.Empty<File>();

            File GenerateFile(
                string id, Uri fileLink, string title, string? group,
                string? itemTitle, int? fileEp, File? dependedOnFile = null, List<File>? filePlaylist = null)
            {
                fileEp ??= fileLink.Segments.Skip(3).FirstOrDefault()?.Split('-')
                    .Select(p => p.GetDigits().ToIntOrNull())
                    .FirstOrDefault(p => p.HasValue);
                var file = new SAFile(Site, id)
                {
                    Title = title,
                    FrameLink = fileLink,
                    Episode = fileEp.ToRange(),
                    Group = group,
                    ItemTitle = itemTitle
                };
                if (filePlaylist == null)
                {
                    filePlaylist = new List<File>();
                    filePlaylist.AddRange(playlist
                        .Select(l => fileLink.ToString().StartsWith(l.ToString().TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                            ? file
                            : GenerateFile(
                                parentId + "_" + l.Segments.Last().Split('-').Last().GetDigits(),
                                l, title, group, itemTitle, null, file, filePlaylist)));
                }
                file.Playlist = filePlaylist;

                file.SetVideosFactory(async (file, token) =>
                {
                    if (dependedOnFile != null)
                    {
                        file.ItemInfo = dependedOnFile.ItemInfo;
                        var tasks = new List<Task>();

                        if (dependedOnFile.Parent?.Parent is Folder itemFolder)
                        {
                            file.Parent = itemFolder.ItemsSource.OfType<SAFolder>().FirstOrDefault(f => f.Link == fileLink);
                        }

                        Task<IEnumerable<File>>? folderPreloadingTask = null;
                        var saFolder = file.Parent as SAFolder;
                        if (saFolder != null)
                        {
                            folderPreloadingTask = LoadTranslateFilesWithTypesAsync(saFolder, token);
                            tasks.Add(folderPreloadingTask);
                        }

                        var newfileLinkTask = saSiteProvider.HttpClient
                            .GetBuilder(fileLink)
                            .WithHeader("Referer", dependedOnFile.FrameLink?.ToString() ?? string.Empty)
                            .SendAsync(token);
                        tasks.Add(newfileLinkTask);

                        await Task.WhenAll(tasks).ConfigureAwait(false);

                        var newfileLink = newfileLinkTask
                            .Result?
                            .RequestMessage?
                            .RequestUri;
                        if (newfileLink != null)
                        {
                            fileLink = newfileLink;
                            ((SAFile)file).SetId(parentId + "_" + newfileLink.ToString().Split('-').Last().GetDigits());
                        }
                        if (folderPreloadingTask != null
                            && saFolder != null)
                        {
                            var items = folderPreloadingTask.Result.Select(n =>
                            {
                                if (n.Id == file.Id)
                                {
                                    file.Title = n.Title;
                                    file.ItemTitle = n.ItemTitle;
                                    file.Group = n.Group;
                                    file.ItemInfo = n.ItemInfo ?? dependedOnFile.ItemInfo;
                                    return file;
                                }

                                return n;
                            });
                            saFolder.Clear();
                            saFolder.AddRange(items);
                        }
                    }
                    var playerFile = await saPlayerParseProvider
                        .ParseFromUriAsync(fileLink, token)
                        .ConfigureAwait(false);
                    if (playerFile == null)
                    {
                        return Enumerable.Empty<Video>();
                    }

                    if (playerFile.SubtitleTracks.Any())
                    {
                        var subLang = GetSubtitlesLanguageFromLink(link);
                        file.SubtitleTracks.AddRange(playerFile.SubtitleTracks
                            .Select(s => new SubtitleTrack(subLang ?? s.Language, s.Link) { Title = s.Title })
                            .ToArray());
                    }

                    return playerFile.Videos;
                });
                return file;
            }
        }

        private static Uri GenerateSAFromShikimoriLink(Uri domain, Uri link)
        {
            var args = QueryStringHelper.CreateQueryString(new Dictionary<string, string?> { ["q"] = link.ToString() });
            return new Uri(domain, "/catalog/search?" + args);
        }

        private static string? GetSubtitlesLanguageFromLink(Uri link)
        {
            var linkStr = link.ToString();
            if (linkStr.Contains("angliyskie-subtitry"))
            {
                return LocalizationHelper.EnLang;
            }

            if (linkStr.Contains("russkie-subtitry"))
            {
                return LocalizationHelper.RuLang;
            }

            if (linkStr.Contains("yaponskie-subtitry"))
            {
                return LocalizationHelper.JpLang;
            }

            return null;
        }

        private static string GetFriendlyTranslationTypeTitle(string title)
        {
            return string.Equals(title, "raw", StringComparison.OrdinalIgnoreCase)
                ? "Оригинал"
                : title;
        }

        private class SAFolder : Folder
        {
            public SAFolder(Site site, string id, Uri link, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                Playlist = Enumerable.Empty<Uri>();
                Link = link;
            }

            public Uri Link { get; }

            public string? ItemTitle { get; set; }

            public IEnumerable<Uri> Playlist { get; set; }
        }

        private class SAFile : File
        {
            public SAFile(Site site, string id) : base(site, id)
            {
            }

            public void SetId(string id)
            {
                Id = id;
            }
        }
    }
}

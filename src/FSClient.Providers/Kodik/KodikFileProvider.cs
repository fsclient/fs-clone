namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Html.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class KodikFileProvider : IFileProvider
    {
        private IEnumerable<ItemInfo>? currentItems;

        private readonly KodikSiteProvider siteProvider;
        private readonly KodikPlayerParseProvider playerParseProvider;

        public KodikFileProvider(KodikSiteProvider siteProvider, KodikPlayerParseProvider playerParseProvider)
        {
            this.siteProvider = siteProvider;
            this.playerParseProvider = playerParseProvider;
        }

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => false;

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItems = items;
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            List<ITreeNode> items;

            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (folder is KodikFolder kodikFolder)
            {
                return await
                    LoadTranslateAsync(
                            kodikFolder.Link,
                            kodikFolder.ItemTitle,
                            kodikFolder.Title,
                            kodikFolder.Id,
                            token)
                        .ConfigureAwait(false);
            }
            else if (folder.Count == 0 && currentItems != null)
            {
                items = await currentItems
                    .Where(i => i.Link != null)
                    .Select(i => new Func<CancellationToken, Task<List<ITreeNode>>>(
                        async t => (await LoadRootAsync(i.Link!, i.Title, t)
                                .ConfigureAwait(false))
                            .ToList()))
                    .WhenAny(list => list.Count > 0, new List<ITreeNode>(), token)
                    .ConfigureAwait(false);

                if (items.Count == 1
                    && items[0] is Folder innerFolder)
                {
                    if (innerFolder.ItemsSource.Any())
                    {
                        return innerFolder.ItemsSource;
                    }

                    return await GetFolderChildrenAsync(innerFolder, token).ConfigureAwait(false);
                }
                else
                {
                    return items;
                }
            }

            return Enumerable.Empty<ITreeNode>();
        }

        private async Task<IEnumerable<ITreeNode>> LoadRootAsync(Uri link, string? itemTitle, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            link = new Uri(domain, link);

            if (link.Host.Contains("kodik.top"))
            {
                link = new Uri(link.ToString().Replace("kodik.top", "kodik.cc"));
            }

            var referer = siteProvider.Properties[KodikSiteProvider.KodikRefererKey] ?? link.ToString();
            var page = await siteProvider
                .HttpClient
                .GetBuilder(link)
                .WithHeader("Referer", referer)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            if (page == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var translates = page
                .QuerySelectorAll(".serial-translations-box option")
                .Select(option => (
                    link: option.GetAttribute("value")?.ToUriOrNull(link),
                    title: string.IsNullOrEmpty(option.TextContent) ? itemTitle : option.TextContent,
                    isCurrent: option.HasAttribute("selected")))
                .Where(tuple => tuple.link != null)
                .Select(tuple =>
                {
                    var translateRootId = tuple.link!.Segments.Skip(2).FirstOrDefault()?.Trim('/').ToIntOrNull();

                    if (tuple.isCurrent)
                    {
                        var folder = new Folder(Site, "kdk" + translateRootId, FolderType.Translate, PositionBehavior.Average)
                        {
                            Title = tuple.title
                        };

                        folder.AddRange(ParseTranslate(page, tuple.link, itemTitle, folder.Id));

                        return folder;
                    }

                    return new KodikFolder(Site, "kdk" + translateRootId, tuple.link, FolderType.Translate, PositionBehavior.Average)
                    {
                        ItemTitle = itemTitle,
                        Title = tuple.title
                    };
                })
                .ToList();
            if (translates.Any())
            {
                return translates;
            }

            var translatesMovies = page
                .QuerySelectorAll(".movie-translations-box option")
                .Select(option => (
                    link: option.GetAttribute("value")?.ToUriOrNull(link),
                    title: string.IsNullOrEmpty(option.TextContent) ? itemTitle : option.TextContent,
                    isCurrent: option.HasAttribute("selected")))
                .Where(tuple => tuple.link != null)
                .Select(tuple =>
                {
                    var translateRootId = "kdk" + tuple.link!.Segments.Skip(2).FirstOrDefault()?.Trim('/').ToIntOrNull();

                    if (tuple.isCurrent)
                    {
                        return playerParseProvider.ParseFilm(page, tuple.link, tuple.title, itemTitle, translateRootId);
                    }

                    var file = new File(Site, translateRootId)
                    {
                        FrameLink = tuple.link,
                        Title = tuple.title,
                        ItemTitle = itemTitle
                    };

                    file.SetVideosFactory(async (f, token) =>
                    {
                        var moviePage = await siteProvider.HttpClient.GetBuilder(f.FrameLink!)
                            .WithHeader("Referer", referer)
                            .SendAsync(token)
                            .AsHtml(token)
                            .ConfigureAwait(false);
                        var file = moviePage == null ? null : playerParseProvider.ParseFilm(moviePage, f.FrameLink!, f.Title, f.ItemTitle, f.Id);
                        if (file != null)
                        {
                            await file.PreloadAsync(token).ConfigureAwait(false);
                        }
                        return file?.Videos ?? Array.Empty<Video>();
                    });

                    return file;
                })
                .Where(file => file != null)
                .ToList();
            if (translatesMovies.Any())
            {
                return translatesMovies!;
            }

            var elseRootId = "kdk" + link.Segments.Skip(2).FirstOrDefault()?.Trim('/').ToIntOrNull();

            var seasons = ParseTranslate(page, link, itemTitle, elseRootId);
            if (seasons.Any())
            {
                return seasons;
            }

            if (playerParseProvider.ParseFilm(page, link, itemTitle, itemTitle, elseRootId) is File file)
            {
                return new[] { file };
            }
            return Enumerable.Empty<ITreeNode>();
        }

        private async Task<IEnumerable<ITreeNode>> LoadTranslateAsync(Uri link, string? parentTitle, string? itemTitle, string parentId, CancellationToken cancellationToken)
        {
            if (link.Host.Contains("kodik.top"))
            {
                link = new Uri(link.ToString().Replace("kodik.top", "kodik.cc"));
            }

            var referer = siteProvider.Properties[KodikSiteProvider.KodikRefererKey] ?? link.ToString();
            var page = await siteProvider
                .HttpClient
                .GetBuilder(link)
                .WithHeader("Referer", referer)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (page == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var seasons = ParseTranslate(page, link, itemTitle, parentId);
            if (seasons.Any())
            {
                return seasons;
            }

            if (playerParseProvider.ParseFilm(page, link, parentTitle, itemTitle, parentId) is File file)
            {
                return new[] { file };
            }
            return Enumerable.Empty<ITreeNode>();
        }

        private IEnumerable<ITreeNode> ParseTranslate(IHtmlDocument document, Uri link, string? itemTitle, string parentId)
        {
            return document
                .QuerySelectorAll(".serial-seasons-box option")
                .Select(option => option.GetAttribute("value")?.ToIntOrNull())
                .Where(season => season.HasValue)
                .Select(season =>
                {
                    var folder = new Folder(Site, parentId + "_" + season, FolderType.Season, PositionBehavior.Average)
                    {
                        Title = "Сезон " + season,
                        Season = season
                    };

                    folder.AddRange(document
                        .QuerySelectorAll($".series-options .season-{season} option")
                        .Select(option => (
                            link: option.GetAttribute("value").ToUriOrNull(link),
                            episode: option.TextContent?.Split(' ').FirstOrDefault()?.ToIntOrNull()))
                        .Where(tuple => tuple.link != null && tuple.episode.HasValue)
                        .Select(tuple =>
                        {
                            var id = tuple.link!.Segments.Skip(3).FirstOrDefault()?.Trim('/').ToIntOrNull();
                            var hash = tuple.link.Segments.Skip(4).FirstOrDefault()?.Trim('/');
                            if (id == null || hash == null)
                            {
                                return null;
                            }

                            var file = new File(Site, folder.Id + "_" + id)
                            {
                                Season = season,
                                Episode = tuple.episode.ToRange(),
                                FrameLink = tuple.link,
                                ItemTitle = itemTitle
                            };
                            file.SetVideosFactory((f, token) => playerParseProvider.GetVideosAsync(f.FrameLink!, id.Value, hash, token));

                            return file;
                        })
                        .Where(file => file != null)!);

                    return folder;
                })
                .Where(season => season.Count > 0);
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public enum KodikFolderType
        {
            Translate = 1,
            Season
        }

        public class KodikFolder : Folder
        {
            public KodikFolder(Site site, string id, Uri link, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                Link = link;
            }

            public Uri Link { get; }
            public string? ItemTitle { get; set; }
        }
    }
}

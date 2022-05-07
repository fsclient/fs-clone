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

    public class HDVBFileProvider : IFileProvider
    {
        private readonly HDVBSiteProvider siteProvider;

        public HDVBFileProvider(
            HDVBSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        private IEnumerable<ItemInfo>? currentItems;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItems = items;
        }

        public Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            if (folder == null)
            {
                return Task.FromException<IEnumerable<ITreeNode>>(new ArgumentNullException(nameof(folder)));
            }

            if (folder.Count == 0 && currentItems != null)
            {
                return Task.FromResult(LoadRootFromItems(currentItems));
            }

            return Task.FromResult(Enumerable.Empty<ITreeNode>());
        }

        private IEnumerable<ITreeNode> LoadRootFromItems(IEnumerable<ItemInfo> items)
        {
            return items
                .OfType<HDVBItemInfo>()
                .Where(i => !string.IsNullOrEmpty(i?.SiteId))
                .Select(i =>
                {
                    var translate = !string.IsNullOrWhiteSpace(i.Translate)
                        ? i.Translate
                        : i.Title;

                    if (i.EpisodesPerSeasons.Count > 0)
                    {
                        var translateId = i.Link?.Segments.Skip(2).FirstOrDefault()?.Trim('/');
                        var seasonFolders = i.EpisodesPerSeasons.Select(season =>
                        {
                            var seasonFolder = new Folder(Site, $"{i.SiteId}_{season.Key}", FolderType.Season, PositionBehavior.Average)
                            {
                                Title = "Сезон " + season.Key,
                                Season = season.Key
                            };

                            var episodeFiles = season.Value.Select(episode =>
                            {
                                var query = QueryStringHelper.CreateQueryString(new Dictionary<string, string?>
                                {
                                    ["e"] = episode.ToString(),
                                    ["s"] = season.Key.ToString(),
                                    ["t"] = translateId
                                });
                                var episodeLink = new Uri(i.Link, "?" + query);
                                var episodeFile = new File(Site, $"{seasonFolder.Id}_{episode}")
                                {
                                    Episode = episode.ToRange(),
                                    Season = season.Key,
                                    ItemTitle = i.Title,
                                    FrameLink = episodeLink
                                };
                                episodeFile.SetVideosFactory(LoadVideosAsync);
                                return episodeFile;
                            });

                            seasonFolder.AddRange(episodeFiles);
                            return seasonFolder;
                        });

                        var translateFolder = new Folder(Site, i.SiteId!, FolderType.Translate, PositionBehavior.Average)
                        {
                            Title = translate
                        };
                        translateFolder.AddRange(seasonFolders);
                        return translateFolder;
                    }

                    var file = new File(Site, i.SiteId!)
                    {
                        Title = translate == "Не требуется" ? "Оригинальная озвучка" : translate,
                        ItemTitle = i.Title,
                        FrameLink = i.Link
                    };
                    file.SetVideosFactory(LoadVideosAsync);

                    return (ITreeNode)file;
                })
                .OfType<ITreeNode>()
                .ToList();
        }

        public async Task<IEnumerable<Video>> LoadVideosAsync(File file, CancellationToken cancellationToken)
        {
            if (file.FrameLink is not Uri link)
            {
                return Array.Empty<Video>();
            }

            var referer = siteProvider.Properties[HDVBSiteProvider.HDVBRefererKey] ?? link.ToString();
            var response = await siteProvider
                .HttpClient
                .GetBuilder(link)
                .WithHeader("Referer", referer)
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
            if (response == null)
            {
                return Array.Empty<Video>();
            }

            link = response.RequestMessage?.RequestUri ?? link;
            var pageHtml = await response.AsText().ConfigureAwait(false);
            if (pageHtml == null)
            {
                return Array.Empty<Video>();
            }

            return await LoadVideosAsync(link, pageHtml, cancellationToken)
                .ToArrayAsync()
                .ConfigureAwait(false);
        }

        private static readonly Regex dataConfigRegex = new Regex(@"data-config=(?:'|"")(?<dataConfig>.+?)(?:'|"")\s");

        private async Task<IEnumerable<Video>> LoadVideosAsync(Uri domain, string pageHtml, CancellationToken cancellationToken)
        {
            var mediaMatch = dataConfigRegex.Match(pageHtml);
            var dataConfig = JsonHelper.ParseOrNull<JObject>(mediaMatch.Groups["dataConfig"]?.Value ?? string.Empty);
            var hlsLink = dataConfig?["hls"]?.ToString()
                .Replace("cdn0.info./", "cdn0.my-serials.info/")
                .ToUriOrNull(domain);
            if (hlsLink == null)
            {
                return Enumerable.Empty<Video>();
            }

            var referer = siteProvider.Properties[HDVBSiteProvider.HDVBRefererKey] ?? domain.ToString();
            var fileText = await siteProvider
                .HttpClient
                .GetBuilder(hlsLink)
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", referer)
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            if (fileText != null)
            {
                var lines = fileText.Split('\n');
                return ProviderHelper.ParseVideosFromM3U8(lines, hlsLink)
                    .Select(v =>
                    {
                        v.CustomHeaders.Add("Referer", referer);
                        v.CustomHeaders.Add("Origin", domain.GetOrigin() ?? string.Empty);
                        return v;
                    });
            }
            return Enumerable.Empty<Video>();
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

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

    public class CollapsFileProvider : IFileProvider
    {
        private readonly CollapsSiteProvider siteProvider;

        public CollapsFileProvider(CollapsSiteProvider collapsSiteProvider)
        {
            siteProvider = collapsSiteProvider;
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
                var items = LoadRootFromItems(currentItems).ToList();
                if (items.Count == 1
                    && items[0] is Folder singleRootFolder)
                {
                    return Task.FromResult<IEnumerable<ITreeNode>>(singleRootFolder);
                }
                else
                {
                    return Task.FromResult<IEnumerable<ITreeNode>>(items);
                }
            }

            return Task.FromResult(Enumerable.Empty<ITreeNode>());
        }

        private IEnumerable<ITreeNode> LoadRootFromItems(IEnumerable<ItemInfo> items)
        {
            return items
                .OfType<CollapsItemInfo>()
                .Where(i => !string.IsNullOrEmpty(i?.SiteId))
                .Select(i =>
                {
                    if (i.EpisodesPerSeasons.Count > 0)
                    {
                        var seasonFolders = i.EpisodesPerSeasons.Select(season =>
                        {
                            var seasonFolder = new Folder(Site, $"{i.SiteId}_{season.Key}", FolderType.Season, PositionBehavior.Average)
                            {
                                Title = "Сезон " + season.Key,
                                Season = season.Key
                            };

                            var episodeFiles = season.Value.Select(episode =>
                            {
                                var episodeFile = new File(Site, $"{seasonFolder.Id}_{episode.episode}")
                                {
                                    Episode = episode.episode.ToRange(),
                                    Season = season.Key,
                                    ItemTitle = i.Title,
                                    FrameLink = episode.link
                                };
                                episodeFile.SetVideosFactory(LoadVideosAsync);
                                return episodeFile;
                            });

                            seasonFolder.AddRange(episodeFiles);
                            return seasonFolder;
                        });

                        var rootFolder = new Folder(Site, i.SiteId!, FolderType.Item, PositionBehavior.Max)
                        {
                            Title = i.Title
                        };
                        rootFolder.AddRange(seasonFolders);
                        return rootFolder;
                    }

                    var file = new File(Site, i.SiteId!)
                    {
                        Title = i.Title,
                        ItemTitle = i.Title,
                        FrameLink = i.Link
                    };
                    file.SetVideosFactory(LoadVideosAsync);

                    return (ITreeNode)file;
                })
                .OfType<ITreeNode>()
                .ToList();
        }

        private async Task<IEnumerable<Video>> LoadVideosAsync(File file, CancellationToken cancellationToken)
        {
            if (file.FrameLink is not Uri link)
            {
                return Array.Empty<Video>();
            }

            var referer = siteProvider.Properties[CollapsSiteProvider.CollapsRefererKey] ?? link.ToString();
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

            return LoadVideos(link, pageHtml);
        }

        private static readonly Regex hlsListRegex = new Regex(@"hlsList\s*:\s*(?<hlsList>{.+?})");

        private static IEnumerable<Video> LoadVideos(Uri domain, string pageHtml)
        {
            var mediaMatch = hlsListRegex.Match(pageHtml);
            var dataConfig = JsonHelper.ParseOrNull<JObject>(mediaMatch.Groups["hlsList"]?.Value ?? string.Empty);
            if (dataConfig == null)
            {
                return Enumerable.Empty<Video>();
            }

            return dataConfig
                .Children<JProperty>()
                .Select(t =>
                {
                    if (t.Value.ToUriOrNull(domain) is not Uri link)
                    {
                        return null;
                    }
                    return new Video(link)
                    {
                        Quality = t.Name
                    };
                })
                .Where(v => v != null)!;
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

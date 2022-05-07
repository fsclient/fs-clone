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

    public class LookbaseFileProvider : IFileProvider
    {
        private static readonly Regex fileRegex = new Regex(@"file\s*:\s*""(?<file>.*?)""");
        private static readonly Regex serialRegex = new Regex(@"file\s*:\s*'(?<file>.*?)'");

        private readonly LookbaseSiteProvider siteProvider;
        private readonly PlayerJsParserService playerJsParserService;

        public LookbaseFileProvider(
            LookbaseSiteProvider siteProvider,
            PlayerJsParserService playerJsParserService)
        {
            this.siteProvider = siteProvider;
            this.playerJsParserService = playerJsParserService;
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

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (folder is LookbaseFolder lookbaseFolder)
            {
                var domain = await siteProvider.GetMirrorAsync(token).ConfigureAwait(false);
                var link = new Uri(domain, lookbaseFolder.Link);
                var text = await siteProvider.HttpClient
                    .GetBuilder(link)
                    .WithHeader("Referer", link.ToString())
                    .SendAsync(token)
                    .AsText()
                    .ConfigureAwait(false);

                var fileNode = fileRegex.Match(text ?? "").Groups["file"].Value.NotEmptyOrNull()
                    ?? serialRegex.Match(text ?? "").Groups["file"].Value;

                fileNode = await playerJsParserService.DecodeAsync(fileNode, siteProvider.PlayerJsConfig, token).ConfigureAwait(false);

                if (fileNode != null)
                {
                    return ProviderHelper.ParsePlaylistFromPlayerJsString(
                        siteProvider.HttpClient, Site,
                        lookbaseFolder.Id, fileNode,
                        lookbaseFolder.Translation, lookbaseFolder.Title,
                        link,
                        LocalizationHelper.RuLang);
                }
            }
            else if (folder.Count == 0)
            {
                var nodes = currentItems
                    .Where(i => i.Link != null)
                    .Select(i => new LookbaseFolder(Site, $"lkbs{i.SiteId}", i.Link!, FolderType.Item, PositionBehavior.Max)
                    {
                        Title = i.Title,
                        Translation = (i as LookbaseItemInfo)?.Translation,
                    })
                    .ToArray();

                if (nodes.Length > 1)
                {
                    return nodes;
                }
                else if (nodes.Length == 1)
                {
                    return await GetFolderChildrenAsync(nodes[0], token).ConfigureAwait(false);
                }
            }

            return Enumerable.Empty<ITreeNode>();
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        private class LookbaseFolder : Folder
        {
            public LookbaseFolder(
                Site site, string id, Uri link, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                Link = link;
            }

            public Uri Link { get; }

            public string? Translation { get; set; }
        }
    }
}

namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class ExFSFileProvider : IFileProvider
    {
        private readonly ExFSSiteProvider siteProvider;

        public ExFSFileProvider(
            ExFSSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => false;

        public bool ProvideTorrent => true;

        public bool ProvideTrailers => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var title = itemInfo?.Title;

            if (string.IsNullOrEmpty(title))
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var html = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "/engine/modules/search-torrents/search.php"))
                .WithBody(new Dictionary<string, string>
                {
                    ["fraza"] = title!,
                    ["search_ok"] = "go_search"
                })
                .WithHeader("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8")
                .WithAjax()
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            var table = html?.QuerySelector(".restable tbody");
            if (table == null)
            {
                return null;
            }

            var nodes = table
                .QuerySelectorAll("tr")
                .Select(tr =>
                {
                    var tds = tr.GetElementsByTagName("td");
                    if (tds.Length < 5
                        || !Uri.TryCreate(domain, tds[4].QuerySelector("a")?.GetAttribute("href"), out var link))
                    {
                        return null;
                    }

                    return new TorrentFolder(Site, "exfs_t_" + link.Segments.Last().Trim('/'), link)
                    {
                        Title = tds[0].TextContent.Trim(),
                        Size = tds[1].TextContent.Trim(),
                        Seeds = tds[2].TextContent?.GetDigits().ToIntOrNull(),
                        Peers = tds[3].TextContent?.GetDigits().ToIntOrNull()
                    };
                })
                .Where(n => n != null)
                .Cast<ITreeNode>();

            var folder = new Folder(Site, $"exfs_t_{itemInfo!.SiteId}", FolderType.Item, PositionBehavior.Average);
            folder.AddRange(nodes);
            return folder;
        }

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            throw new NotSupportedException();
        }
    }
}

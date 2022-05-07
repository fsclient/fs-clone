namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class KinoUkrFileProvider : IFileProvider
    {
        private ItemInfo? currentItem;

        private readonly KinoUkrSiteProvider siteProvider;
        private readonly TortugaFileProvider tortugaFileProvider;
        private readonly IPlayerParseManager playerParseManager;

        public KinoUkrFileProvider(
            KinoUkrSiteProvider siteProvider,
            TortugaFileProvider tortugaFileProvider,
            IPlayerParseManager playerParseManager)
        {
            this.siteProvider = siteProvider;
            this.tortugaFileProvider = tortugaFileProvider;
            this.playerParseManager = playerParseManager;
        }

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => true;

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

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

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var relativeLink = itemInfo.Link
                ?? new Uri($"/{itemInfo.SiteId}-placeholder.html", UriKind.Relative);
            var html = await siteProvider.HttpClient
                .GetBuilder(new Uri(domain, relativeLink))
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            var id = "ku" + itemInfo.SiteId + "_t";

            IEnumerable<File>? files = null;

            var youTubeLink = html?.QuerySelector("#dle-content iframe[src*=youtu]")?
                .GetAttribute("src")?
                .ToUriOrNull();
            if (youTubeLink != null
                && playerParseManager.CanOpenFromLinkOrHostingName(youTubeLink, Sites.Youtube))
            {
                var file = await playerParseManager.ParseFromUriAsync(youTubeLink, Sites.Youtube, cancellationToken).ConfigureAwait(false);
                if (file == null)
                {
                    return null;
                }

                file.IsTrailer = true;
                file.Title ??= itemInfo.Title;
                files = new[] { file };
            }

            var tortugaLink = html?.QuerySelector("#dle-content iframe[src*=tortuga]")?
                .GetAttribute("src")?
                .ToUriOrNull();
            if (tortugaLink != null)
            {
                var nodes = await tortugaFileProvider
                    .GetVideosFromTortugaAsync(tortugaLink, domain, id, itemInfo.Title, null, cancellationToken)
                    .ConfigureAwait(false);
                files = nodes.OfType<File>().Select(f =>
                {
                    f.IsTrailer = true;
                    return f;
                });
            }

            if (files != null)
            {
                var folder = new Folder(Site, $"ku_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
                folder.AddRange(files);
                return folder;
            }
            return null;
        }

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItem = items
                .FirstOrDefault();
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            if (currentItem?.SiteId == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            if (folder.Count > 0)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var domain = await siteProvider.GetMirrorAsync(token).ConfigureAwait(false);

            var relativeLink = currentItem.Link
                ?? new Uri($"/{currentItem.SiteId}-placeholder.html", UriKind.Relative);

            var (iframeLinks, translationTitle) = await GetPlayerIFrameLinkAsync(new Uri(domain, relativeLink), token)
                .ConfigureAwait(false);

            foreach (var link in iframeLinks)
            {
                var items = await tortugaFileProvider
                    .GetVideosFromTortugaAsync(link, domain, folder.Id, currentItem.Title, translationTitle, token)
                    .ToArrayAsync()
                    .ConfigureAwait(false);

                if (items.Length > 0)
                {
                    return items;
                }
            }

            return Enumerable.Empty<ITreeNode>();
        }

        private async Task<(IEnumerable<Uri> links, string? translationTitle)> GetPlayerIFrameLinkAsync(
            Uri itemLink,
            CancellationToken cancellationToken)
        {
            var page = await siteProvider
                .HttpClient
                .GetBuilder(itemLink)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            var tabsContainer = page?.QuerySelector("#dle-content .tabs-box");

            if (page == null || tabsContainer == null)
            {
                return (Array.Empty<Uri>(), null);
            }

            var tabs = tabsContainer.QuerySelectorAll(".tabs-b")
                .Select(e => e.QuerySelector("iframe")?.GetAttribute("src")?.ToUriOrNull(itemLink))
                .ToArray();

            var links = tabsContainer.QuerySelectorAll(".tabs-sel span")
                .Select((elelemnt, index) => (elelemnt, index))
                .Where(t => t.elelemnt.TextContent?.IndexOf("Трейлер", StringComparison.OrdinalIgnoreCase) < 0)
                .Select(t => tabs.Skip(t.index).FirstOrDefault())
                .Where(uri => uri != null);

            var metaUri = page
                .QuerySelector("head > meta[property='ya:ovs:content_id'][content*='/']")?
                .GetAttribute("content")?
                .ToUriOrNull(itemLink);

            var translationTitle = page
                .QuerySelector(".finfo .sd-line:contains('Звук:')")?
                .LastChild?
                .TextContent?
                .Trim();

            if (metaUri != null)
            {
                links = links.Union(new[] { metaUri });
            }

            return (links, translationTitle)!;
        }
    }
}

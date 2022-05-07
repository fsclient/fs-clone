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

    public class YohohoFileProvider : IFileProvider
    {
        private readonly YohohoSiteProvider siteProvider;
        private readonly YohohoSearchProvider searchProvider;
        private readonly IPlayerParseManager playerParseManager;

        public YohohoFileProvider(
            YohohoSiteProvider siteProvider,
            YohohoSearchProvider searchProvider,
            IPlayerParseManager playerParseManager)
        {
            this.siteProvider = siteProvider;
            this.searchProvider = searchProvider;
            this.playerParseManager = playerParseManager;
        }

        public bool ProvideOnline => false;

        public bool ProvideTorrent => true;

        public bool ProvideTrailers => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public Site Site => siteProvider.Site;

        public Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var hasKpId = itemInfo.Details.LinkedIds.ContainsKey(Sites.Kinopoisk);

            var results = await searchProvider.GetRelatedResultsAsync(itemInfo, true, cancellationToken).ConfigureAwait(false);
            if (results.TryGetValue("torrent", out var torrentResult)
                && torrentResult.IFrame?.ToUriOrNull() is Uri iframeLink)
            {
                var referer = new Uri(siteProvider.Properties[YohohoSiteProvider.YohohoRefererKey], UriKind.Absolute);

                var html = await siteProvider.HttpClient
                    .PostBuilder(iframeLink)
                    .WithHeader("Origin", referer.GetOrigin())
                    .WithHeader("Referer", referer.ToString())
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .SendAsync(cancellationToken)
                    .AsHtml(cancellationToken)
                    .ConfigureAwait(false);
                if (html == null)
                {
                    return null;
                }

                var allTitles = itemInfo.GetTitles().ToArray();

                var nodes = html.QuerySelectorAll("table.torrent tr")
                    .Select(res =>
                    {
                        var title = res.QuerySelector("td:nth-child(3) span")?.TextContent?.Trim();
                        var size = res.QuerySelector("td:nth-child(4) div")?.TextContent?.Trim();
                        var magnetLink = res.QuerySelector("a[href*=magnet]")?.GetAttribute("href")?.ToUriOrNull();

                        if (magnetLink != null)
                        {
                            return new TorrentFolder(Site, "tl" + magnetLink.AbsoluteUri.GetDeterministicHashCode(), magnetLink)
                            {
                                Title = title,
                                Size = size
                            };
                        }
                        else
                        {
                            return null;
                        }
                    })
                    .Where(torrent => torrent?.Title != null
                        && (hasKpId || Filter(torrent, allTitles, itemInfo.Details.Year, itemInfo.Details.YearEnd, itemInfo.Section.Modifier)))
                    .Distinct()
                    .OfType<ITreeNode>();

                var folder = new Folder(Site, $"yhh_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
                folder.AddRange(nodes);
                return folder;
            }

            return null;
        }

        public async Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var results = await searchProvider.GetRelatedResultsAsync(itemInfo, true, cancellationToken).ConfigureAwait(false);
            if (results.TryGetValue("trailer", out var trailerResult)
                && trailerResult.IFrame?.ToUriOrNull() is Uri iframeLink
                && playerParseManager.CanOpenFromLinkOrHostingName(iframeLink, Site.Any))
            {
                var file = await playerParseManager.ParseFromUriAsync(iframeLink, Site.Any, cancellationToken).ConfigureAwait(false);
                if (file == null)
                {
                    return null;
                }

                file.IsTrailer = true;

                var folder = new Folder(Site, $"yhh_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
                folder.Add(file);
                return folder;
            }

            return null;
        }

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            throw new NotImplementedException();
        }

        private static bool Filter(TorrentFolder torrent, ICollection<string> titles, int? startYear, int? endYear, SectionModifiers sectionModifiers)
        {
            var torrentTitle = torrent.Title!;

            if (startYear.HasValue
                && !torrentTitle.Contains(startYear.ToString()))
            {
                if (sectionModifiers.HasFlag(SectionModifiers.Serial)
                    || sectionModifiers.HasFlag(SectionModifiers.TVShow))
                {
                    var hasAnyYear = Enumerable.Range(startYear.Value, (endYear ?? DateTime.Now.Year) - startYear.Value + 1)
                        .Any(year => torrentTitle.Contains(year.ToString()));

                    if (!hasAnyYear)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            var separators = new[] { '/', '\\', '[', ']', '(', ')', ';' }
                .Where(separator => titles.All(title => !title.Contains(separator)))
                .ToArray();
            var parts = torrentTitle.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            var maxProximity = parts.MaxOrDefault(part => titles.MaxOrDefault(title => title.Proximity(part.GetLettersAndDigits(), false)));

            return maxProximity >= 0.87;
        }
    }
}

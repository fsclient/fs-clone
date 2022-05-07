namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Html.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class TorLookFileProvider : IFileProvider
    {
        private static readonly Regex ignoreRegex = new Regex(
            @"\b(?:FB2|EPUB|PDF|DOC|MP3|FLAC|APE|RePack)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly TorLookSiteProvider siteProvider;

        public TorLookFileProvider(
            TorLookSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => false;

        public bool ProvideTorrent => true;

        public bool ProvideTrailers => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
        }

        public async Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (itemInfo == null)
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var allTitles = itemInfo.GetTitles().Select(title => title.GetLettersAndDigits()).ToList();
            var originTitles = itemInfo.GetTitles(true, false).ToList();

            var requests = (originTitles.Count > 0
                ? itemInfo.Details.Titles
                    .Concat(new[] { string.Empty })
                    .SelectMany(ruTitle => originTitles
                        .Select(enTitle => Uri.EscapeDataString($"{ruTitle} {enTitle}".Trim())))
                : allTitles.Select(title => Uri.EscapeDataString(title)))
                .Select(request => (
                    success: Uri.TryCreate(domain, request, out var link),
                    link
                ))
                .Where(tuple => tuple.success)
                .Select(tuple => tuple.link);

            var nodes = await requests
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((request, ct) => new ValueTask<IHtmlDocument?>(siteProvider.HttpClient
                    .GetBuilder(request)
                    .SendAsync(ct)
                    .AsHtml(ct)))
                .Where(page => page != null)
                .SelectMany(page => page!.QuerySelectorAll(".webResult").ToAsyncEnumerable())
                .Select(res =>
                {
                    var title = res.QuerySelector("p a")?.TextContent?.Trim();
                    var size = res.QuerySelector(".size")?.TextContent?.Trim();
                    var group = res.QuerySelector(".h2 a[href]")?.TextContent?.Trim();
                    var seeds = res.QuerySelector(".seeders")?.TextContent?.ToIntOrNull();
                    var leeches = res.QuerySelector(".leechers")?.TextContent?.ToIntOrNull();

                    var anchorElement = res.QuerySelector("a.magneto");
                    if (anchorElement?.GetAttribute("href")?.ToUriOrNull(UriKind.Absolute) is Uri link
                        && link.Scheme != "javascript")
                    {
                        return new TorrentFolder(Site, "tl" + link.AbsoluteUri.GetDeterministicHashCode(), link)
                        {
                            Title = title,
                            Size = size,
                            Group = group,
                            Seeds = seeds,
                            Leeches = leeches
                        };
                    }
                    else if (anchorElement?.GetAttribute("data-src") is string dataSrc
                        && Uri.TryCreate(domain, dataSrc, out var dataLink))
                    {
                        return new TorrentFolder(Site, "tl" + dataSrc.GetDeterministicHashCode(), async ct =>
                        {
                            var pageText = await siteProvider.HttpClient
                               .GetBuilder(dataLink)
                               .SendAsync(ct)
                               .AsText()
                               .ConfigureAwait(false);
                            return Regex.Match(pageText ?? string.Empty, "href='(?<link>.+?)'")
                                .Groups["link"]
                                .Value
                                .ToUriOrNull();
                        })
                        {
                            Title = title,
                            Size = size,
                            Group = group,
                            Seeds = seeds,
                            Leeches = leeches
                        };
                    }
                    else
                    {
                        return null;
                    }
                })
                .Where(torrent => torrent?.Title != null
                    && Filter(torrent, allTitles, itemInfo.Details.Year, itemInfo.Details.YearEnd, itemInfo.Section.Modifier))
                .Distinct()
                .OrderByDescending(torrent => torrent!.Seeds ?? 0)!
                .OfType<ITreeNode>()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var folder = new Folder(Site, $"tl_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
            folder.AddRange(nodes);
            return folder;
        }

        public Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
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

            var hasIgnoredWordInRequest = titles.Any(title => ignoreRegex.IsMatch(title));
            if (!hasIgnoredWordInRequest
                && ignoreRegex.IsMatch(torrentTitle))
            {
                return false;
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

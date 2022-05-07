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

    using Humanizer;

    using Nito.AsyncEx;

    public class ThePirateBayFileProvider : IFileProvider
    {
        private readonly ThePirateBaySiteProvider siteProvider;
        private readonly AsyncLazy<(IEnumerable<string> trackers, Uri? apiDomain)> trackersLazyTask;

        public ThePirateBayFileProvider(
            ThePirateBaySiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;

            trackersLazyTask = new AsyncLazy<(IEnumerable<string>, Uri?)>(GetTrackersAsync);
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
            if (itemInfo?.Title == null)
            {
                return null;
            }
            var (trackers, apiDomain) = await trackersLazyTask.ConfigureAwait(false);
            if (!trackers.Any())
            {
                return null;
            }

            apiDomain ??= siteProvider.Properties[ThePirateBaySiteProvider.ApiDomainKey].ToUriOrNull();
            var titles = itemInfo.GetTitles(true).ToArray();

            var json = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(apiDomain, "q.php"))
                .WithArgument("q", itemInfo.Title)
                // 200 for "videos"
                .WithArgument("cat", "200")
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JArray>()
                .ConfigureAwait(false);
            if (json == null)
            {
                return null;
            }

            var nodes = json
                .Select(res =>
                {
                    var id = res["id"]?.ToString();
                    var hash = res["info_hash"]?.ToString();
                    var title = res["name"]?.ToString();
                    var size = res["size"]?.ToLongOrNull()?.Bytes().ToString("0.000");
                    var seeds = res["seeders"]?.ToIntOrNull();
                    var leeches = res["leechers"]?.ToIntOrNull();
                    var group = FormatCategory(res["category"]?.ToIntOrNull());
                    var link = FormatMagnet(hash, title, trackers);
                    if (id == null || link == null || title == null)
                    {
                        return null;
                    }

                    return new TorrentFolder(Site, "tpb" + id, link)
                    {
                        Title = title,
                        Size = size,
                        Group = group,
                        Seeds = seeds,
                        Leeches = leeches
                    };
                })
                .Where(torrent => torrent?.Title != null
                    && Filter(torrent, titles, itemInfo.Details.Year, itemInfo.Details.YearEnd, itemInfo.Section.Modifier))
                .Distinct()
                .OrderByDescending(torrent => torrent!.Seeds ?? 0)!
                .OfType<ITreeNode>()
                .ToList();

            var folder = new Folder(Site, $"tpb_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
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

        private async Task<(IEnumerable<string>, Uri?)> GetTrackersAsync()
        {
            var domain = await siteProvider.GetMirrorAsync(default)
                .ConfigureAwait(false);
            var scriptText = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, "static/main.js"))
                .SendAsync(default)
                .AsText()
                .ConfigureAwait(false);
            if (scriptText == null)
            {
                return (Enumerable.Empty<string>(), null);
            }

            var apiDomain = Regex.Match(scriptText, @"\bserver='(?<apiDomain>http.+?)'").Groups["apiDomain"]?.Value?.ToUriOrNull();
            var printTrackersBody = Regex.Match(scriptText, @"\bprint_trackers\(\){(?<printTrackersBody>.+)}").Groups["printTrackersBody"]?.Value;
            if (printTrackersBody == null)
            {
                return (Enumerable.Empty<string>(), apiDomain);
            }

            var trackers = Regex.Matches(printTrackersBody, @"encodeURIComponent\('(?<tracker>.+?)'\)")
                .OfType<Match>()
                .Select(m => m.Groups["tracker"])
                .Where(t => t.Success && !string.IsNullOrEmpty(t.Value))
                .Select(t => t.Value)
                .ToArray();

            return (trackers, apiDomain);
        }

        private static bool Filter(TorrentFolder torrent, ICollection<string> titles, int? startYear, int? endYear, SectionModifiers sectionModifiers)
        {
            return true;
        }

        private static string? FormatCategory(int? cat)
        {
            if (cat == null || cat == 0)
            {
                return null;
            }

            var cc = cat.ToString();
            var main = cc.FirstOrDefault() switch
            {
                '1' => "Audio",
                '2' => "Video",
                '3' => "Applications",
                '4' => "Games",
                '5' => "Porn",
                '6' => "Other",
                _ => string.Empty
            };

            return main + " > " + (cat switch
            {
                101 => "Music" ,
                102 => "Audio Books" ,
                103 => "Sound clips" ,
                104 => "FLAC" ,
                199 => "Other" ,
                201 => "Movies" ,
                202 => "Movies DVDR" ,
                203 => "Music videos" ,
                204 => "Movie Clips" ,
                205 => "TV-Shows" ,
                206 => "Handheld" ,
                207 => "HD Movies" ,
                208 => "HD TV-Shows" ,
                209 => "3D" ,
                299 => "Other" ,
                301 => "Windows" ,
                302 => "Mac/Apple" ,
                303 => "UNIX" ,
                304 => "Handheld" ,
                305 => "IOS(iPad/iPhone)" ,
                306 => "Android" ,
                399 => "Other OS" ,
                401 => "PC" ,
                402 => "Mac/Apple" ,
                403 => "PSx" ,
                404 => "XBOX360" ,
                405 => "Wii" ,
                406 => "Handheld" ,
                407 => "IOS(iPad/iPhone)" ,
                408 => "Android" ,
                499 => "Other OS" ,
                501 => "Movies" ,
                502 => "Movies DVDR" ,
                503 => "Pictures" ,
                504 => "Games" ,
                505 => "HD-Movies" ,
                506 => "Movie Clips" ,
                599 => "Other" ,
                601 => "E-books" ,
                602 => "Comics" ,
                603 => "Pictures" ,
                604 => "Covers" ,
                605 => "Physibles" ,
                699 => "Other" ,
                _ => string.Empty
            });
        }

        private static Uri? FormatMagnet(string? hash, string? name, IEnumerable<string> trackers)
        {
            if (hash == null)
            {
                return null;
            }

            var str = $"magnet:?xt=urn:btih:{hash}&dn={Uri.EscapeDataString(name ?? string.Empty)}{string.Concat(trackers.Select(t => $"&tr={Uri.EscapeDataString(t)}"))}";
            return str.ToUriOrNull();
        }
    }
}

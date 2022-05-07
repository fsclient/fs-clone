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

    using Newtonsoft.Json.Linq;

    using Nito.AsyncEx;

    public class KodikPlayerParseProvider : IPlayerParseProvider
    {
        private readonly KodikSiteProvider siteProvider;
        private readonly SemaphoreSlim hash2Semaphore;
        private (string? hash, string? relativeLink) cache;

        public KodikPlayerParseProvider(KodikSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
            hash2Semaphore = new SemaphoreSlim(1);
        }

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public Site Site => siteProvider.Site;

        public bool CanOpenFromLinkOrHostingName(Uri link)
        {
            // https://aniqit.com/seria/687506/ac4452ecb58e66d6d46444c9611cc376/720p
            // http://kodik.cc/video/1704/f89980fbd13e8154d3ea137848fd8350/720p
            // http://kodik.info/go/seria/454701/3fbfea0db147576d7328b3f939954f5a/720p
            var segments = link.Segments.SkipWhile(s => s == "/" || s == "go/").ToArray();
            return segments.Length == 4
                && (segments[0] == "seria/" || segments[0] == "serial/" || segments[0] == "video/")
                && segments[3].EndsWith("p", StringComparison.Ordinal);
        }

        public async Task<File?> ParseFromUriAsync(Uri link, CancellationToken cancellationToken)
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
                return null;
            }

            var rootId = "kdk" + link.Segments.Skip(2).FirstOrDefault()?.Trim('/').ToIntOrNull();
            var title = page.Title?.NotEmptyOrNull() ?? "Kodik Player";

            return ParseFilm(page, link, title, title, rootId);
        }

        internal File? ParseFilm(IHtmlDocument document, Uri link, string? translation, string? itemTitle, string parentId)
        {
            var iframeLink = document.QuerySelectorAll("iframe[src]")
                .LastOrDefault()?
                .GetAttribute("src")?
                .ToUriOrNull(link);

            if (iframeLink == null)
            {
                iframeLink = Regex.Match(string.Concat(document.Scripts.Select(s => s.Text)), @"src\s*=\s*""(?<src>.+?(?:(?:video)|(?:seria)).+?)""")
                    .Groups["src"]
                    .Value
                    .ToUriOrNull(link);
            }

            if (iframeLink == null)
            {
                return null;
            }

            var id = iframeLink.Segments.Skip(3).FirstOrDefault()?.Trim('/').ToIntOrNull();
            var hash = iframeLink.Segments.Skip(4).FirstOrDefault()?.Trim('/');
            if (!id.HasValue || string.IsNullOrEmpty(hash))
            {
                return null;
            }

            var file = new File(Site, parentId)
            {
                FrameLink = iframeLink,
                Title = translation,
                ItemTitle = itemTitle
            };

            file.SetVideosFactory((f, token) => GetVideosAsync(f.FrameLink!, id.Value, hash!, token));

            return file;
        }

        private async Task<(string? hash, string? relativeLink)> GetHash2AndRelativeLinkAsync(Uri iframeLink, CancellationToken cancellationToken)
        {
            if (cache.relativeLink != null)
            {
                return cache;
            }

            using var _ = await hash2Semaphore.LockAsync(cancellationToken);

            if (cache.relativeLink != null)
            {
                return cache;
            }

            var page = await siteProvider.HttpClient
                .GetBuilder(iframeLink)
                .WithHeader("Origin", iframeLink.GetOrigin())
                .WithHeader("Referer", iframeLink.ToString())
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);
            if (page == null)
            {
                return default;
            }

            var scriptLink = Regex.Match(page, @"script.+?src=""(?<scriptLink>.+?app\.promo.+?)""").Groups["scriptLink"].Value.NotEmptyOrNull();
            if (scriptLink == null)
            {
                return default;
            }

            var script = await siteProvider.HttpClient
                .GetBuilder(new Uri(iframeLink, scriptLink))
                .WithHeader("Origin", iframeLink.GetOrigin())
                .WithHeader("Referer", iframeLink.ToString())
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            return cache = (
                Regex.Match(script, @"hash2:""(?<hash>.+?)""").Groups["hash"].Value.NotEmptyOrNull(),
                Regex.Match(script, @"getPlayerData\(\){.+?url:""(?<relativeLink>.+?)""").Groups["relativeLink"].Value.NotEmptyOrNull()
            );
        }

        internal async Task<IEnumerable<Video>> GetVideosAsync(Uri iframeLink, int videoId, string hash, CancellationToken cancellationToken)
        {
            if (iframeLink == null)
            {
                return Enumerable.Empty<Video>();
            }
            var usingCachedHash = cache.hash != null;
            var (hash2, relativeLink) = await GetHash2AndRelativeLinkAsync(iframeLink, cancellationToken).ConfigureAwait(false);

            var args = QueryStringHelper.ParseQuery(iframeLink.Query).ToDictionary(p => p.Key, p => p.Value);
            if (!args.ContainsKey("hash"))
            {
                args.Add("hash", hash);
            }
            if (!args.ContainsKey("hash2"))
            {
                args.Add("hash2", hash2 ?? string.Empty);
            }

            if (!args.ContainsKey("id"))
            {
                args.Add("id", videoId.ToString());
            }

            if (!args.ContainsKey("type"))
            {
                args.Add("type", iframeLink.ToString().Contains("seria") ? "seria" : "video");
            }

            if (args.ContainsKey("bad_user"))
            {
                args["bad_user"] = "false";
            }
            else
            {
                args.Add("bad_user", "false");
            }

            if (siteProvider.Properties.TryGetValue(KodikSiteProvider.GetVideosRelativeLinkKey, out var relativeLinkOverride))
            {
                relativeLink = relativeLinkOverride;
            }

            var json = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(iframeLink, relativeLink))
                .WithBody(args)
                .WithHeader("Origin", iframeLink.GetOrigin())
                .WithHeader("Referer", iframeLink.ToString())
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            if (json == null)
            {
                return Enumerable.Empty<Video>();
            }

            if (json["links"] is JObject links)
            {
                var directLinks = links
                    .Properties()
                    .Select(videoNode => (
                        qual: (Quality)videoNode.Name,
                        link: (((videoNode.Value as JArray)?.FirstOrDefault() ?? videoNode.Value) as JObject)?["src"]?.ToUriOrNull(iframeLink)))
                    .Where(tuple => !tuple.qual.IsUnknown && tuple.link != null && !tuple.link.PathAndQuery.Contains("Sintel.mp4"))
                    .GroupBy(tuple => tuple.link)
                    .Select(group => group.OrderBy(tuple => tuple.qual).First())
                    .Select(tuple => new Video(tuple.link!) { Quality = tuple.qual })
                    .ToArray();

                if (directLinks.Length == 0)
                {
                    cache = default;
                    if (usingCachedHash)
                    {
                        return await GetVideosAsync(iframeLink, videoId, hash, cancellationToken).ConfigureAwait(false);
                    }
                }

                return directLinks;
            }

            if (json["link"]?.ToUriOrNull(iframeLink) is Uri link)
            {
                var m3u8Playlist = await siteProvider
                    .HttpClient
                    .GetBuilder(link)
                    .WithAjax()
                    .SendAsync(cancellationToken)
                    .AsText()
                    .ConfigureAwait(false);
                if (m3u8Playlist != null)
                {
                    return ProviderHelper.ParseVideosFromM3U8(m3u8Playlist.Split('\n'));
                }
            }

            return Enumerable.Empty<Video>();
        }

    }
}

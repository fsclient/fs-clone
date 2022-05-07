namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    /// <inheritdoc/>
    public class SmotretAnimePlayerParseProvider : IPlayerParseProvider
    {
        private readonly SmotretAnimeSiteProvider siteProvider;

        public SmotretAnimePlayerParseProvider(
            SmotretAnimeSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.ProForAny;

        public bool CanOpenFromLinkOrHostingName(Uri link)
        {
            var hosting = link.Host.GetLettersAndDigits().ToLower();
            var rootDomain = link.GetRootDomain();

            return hosting.Contains("smotretanime")
                || hosting.EndsWith("365ru", StringComparison.Ordinal)
                || siteProvider.Mirrors.Any(m => m.GetRootDomain() == rootDomain);
        }

        public async Task<File?> ParseFromUriAsync(Uri httpUri, CancellationToken cancellationToken)
        {
            if (!httpUri.AbsolutePath.Contains("embed"))
            {
                var embedId = httpUri.Segments
                    .LastOrDefault()?
                    .Split('-', '/')
                    .LastOrDefault()?
                    .GetDigits()
                    .ToIntOrNull();
                if (!embedId.HasValue)
                {
                    return null;
                }

                httpUri = new Uri(httpUri, "/translations/embed/" + embedId);
            }

            var text = await siteProvider.HttpClient
                .GetBuilder(httpUri)
                .WithCookies(new[]
                {
                    new Cookie("watchedVideoToday", "1")
                })
                .WithHeader("Referer", httpUri.ToString())
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false)
                ?? string.Empty;

            var match = Regex.Match(text, @"<video\s+?(?=.*id\s*=\s*.main-video.)(?=.*data-id=""(?<id>\d+)"")(?=(?:.*data-vtt=""(?<vtt>.*?)"")?)(?=.*data-subtitles=""(?<subs>.*?)"")(?=.*data-sources=""(?<sources>.*?)"")(?=(?:.*data-alternative-sources=""(?<altsources>.*?)"")?).*?>");

            var id = match.Groups["id"].Value.ToIntOrNull();
            var subsLinks = new[] {
                    (name: LocalizationHelper.RuLang + ": WEBVTT", link: match.Groups["vtt"].Value.ToUriOrNull(httpUri)),
                    (null, match.Groups["subs"].Value.ToUriOrNull(httpUri))}
                .Where(t => t.link != null)
                .Select((t, index) => new SubtitleTrack(LocalizationHelper.RuLang, t.link!) { Title = t.name })
                .ToArray();

            var srcText = WebUtility.HtmlDecode(match.Groups["sources"].Value);
            var altSrcText = WebUtility.HtmlDecode(match.Groups["altsources"].Value);

            var sources = JsonHelper.ParseOrNull<JArray>(srcText)?
               .Concat(JsonHelper.ParseOrNull<JArray>(altSrcText) ?? new JArray())
               ?? new JArray();

            var videos = sources
                .Select(video => (
                    qual: video["height"]?.ToIntOrNull() ?? 0,
                    urls: (video["urls"] as JArray)?
                        .Select(u => u.ToUriOrNull(httpUri))
                        .Where(l => l != null)?
                        .ToArray() ?? Array.Empty<Uri>()))
                .Where(t => t.urls.Length > 0)
                .Select(t => (
                    t.qual,
                    variant: new VideoVariant(t.urls!),
                    downUrl: t.urls.Length == 1 && Uri.TryCreate(httpUri, $"/translations/mp4/{id}?download=1&height={t.qual}", out var temp) ? temp : null))
                .GroupBy(t => (t.qual, t.downUrl), t => t.variant)
                .Select(t => new Video(t.OrderBy(v => v.Parts.First()!.Host.Contains("google")))
                {
                    Quality = t.Key.qual,
                    DownloadLink = t.Key.downUrl
                })
                .Select(link =>
                {
                    if (link.Links.Count == 1
                        && link.DownloadLink != null
                        && link.DownloadLink != link.SingleLink)
                    {
                        var cooks = siteProvider.Handler.GetCookies(link.DownloadLink);
                        link.CustomHeaders.Add("Cookie", CookieHelper.ToCookieString(cooks));
                    }
                    return link;
                })
                .ToArray();

            var file = new File(Site, "san" + GetIdFromLink(httpUri));
            file.FrameLink = httpUri;
            file.SubtitleTracks.AddRange(subsLinks);
            file.SetVideos(videos);

            return file;
        }

        private static string GetIdFromLink(Uri link)
        {
            // /anime/905-shokugeki-no-souma-shin-no-sara/episode_11-dubbed
            // /embed/episode_905_11-dubbed
            // both result 905_11-dubbed

            var lastSegment = link.Segments.Last().Trim('/').Replace("episode_", "");
            if (!link.AbsolutePath.Contains("embed"))
            {
                var itemId = link.Segments.Reverse().Skip(1).FirstOrDefault()?.Split('-').First();
                if (itemId != null)
                {
                    return itemId + "_" + lastSegment;
                }
            }
            return lastSegment;
        }
    }
}

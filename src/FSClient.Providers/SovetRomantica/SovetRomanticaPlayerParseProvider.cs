namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    /// <inheritdoc/>
    public class SovetRomanticaPlayerParseProvider : IPlayerParseProvider
    {
        private readonly SovetRomanticaSiteProvider siteProvider;

        public SovetRomanticaPlayerParseProvider(
            SovetRomanticaSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLinkOrHostingName(Uri link)
        {
            var rootDomain = link.GetRootDomain();
            return siteProvider.Mirrors.Any(m => m.GetRootDomain() == rootDomain);
        }

        public async Task<File?> ParseFromUriAsync(Uri httpUri, CancellationToken cancellationToken)
        {
            var pageText = await siteProvider.HttpClient
                .GetBuilder(httpUri)
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);
            if (pageText == null)
            {
                return null;
            }

            var link = Regex
                .Matches(pageText, @"(?<src>\.src)?\s*=?\(?\s*(?:'|"")(?<link>[^'""]{10,}\.(?:mp4|m3u8).*?)(?:'|"")\s*\)?")
                .OfType<Match>()
                .OrderBy(m => m.Groups["src"].Success)
                .FirstOrDefault()?
                .Groups["link"]
                .Value
                .ToUriOrNull(httpUri);
            if (link == null)
            {
                return null;
            }

            var videos = Array.Empty<Video>();
            if (link.AbsoluteUri.Contains("mp4"))
            {
                videos = new[] { new Video(link)
                {
                    CustomHeaders =
                    {
                        ["Referer"] = "https://sovetromantica.com/",
                        ["User-Agent"] = WebHelper.DefaultUserAgent
                    }
                }};
            }
            else if (link.AbsoluteUri.Contains("m3u8"))
            {
                var fileText = await siteProvider.HttpClient
                    .GetBuilder(link)
                    .SendAsync(cancellationToken)
                    .AsText()
                    .ConfigureAwait(false);
                if (fileText != null)
                {
                    videos = ProviderHelper
                        .ParseVideosFromM3U8(fileText.Split('\n'), link)
                        .ToArray();
                }
            }
            else
            {
                return null;
            }

            var file = new File(Site, "sovrom" + GetIdFromLink(httpUri));
            file.FrameLink = httpUri;
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

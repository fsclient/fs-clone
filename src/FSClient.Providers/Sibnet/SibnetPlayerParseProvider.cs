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
    public class SibnetPlayerParseProvider : IPlayerParseProvider
    {
        private readonly SibnetSiteProvider siteProvider;

        public SibnetPlayerParseProvider(
            SibnetSiteProvider siteProvider)
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
            var text = await siteProvider.HttpClient
                .GetBuilder(httpUri)
                .WithHeader("User-Agent", WebHelper.MobileUserAgent)
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var mirror = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var videos = Regex
                .Matches(text, @"(?:src|'file')\s*:\s*(?:\""|')(?<link>.{10,}?\.(?:mp4|m3u8|mpd)[^""']*)")
                .Cast<Match>()
                .Select(match => match.Groups["link"].Value.ToUriOrNull(mirror))
                .Where(link => link != null)
                .GroupBy(link => link)
                .Select(g => g.First())
                .Select(link => new Video(link!)
                {
                    CustomHeaders =
                    {
                        ["Referer"] = httpUri.ToString()
                    }
                })
                .ToArray();

            var id = QueryStringHelper.ParseQuery(httpUri.Query).FirstOrDefault(p => p.Key == "videoid").Value;
            if (string.IsNullOrEmpty(id))
            {
                id = httpUri.AbsolutePath.Split('-').First().GetDigits();
            }

            var file = new File(Site, "sbnet" + id);
            file.FrameLink = httpUri;
            file.SetVideos(videos);

            return file;
        }
    }
}

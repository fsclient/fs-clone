namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    /// <inheritdoc/>
    public class AnimeJoyPlayerParseProvider : IPlayerParseProvider
    {
        private readonly AnimeJoySiteProvider siteProvider;

        public AnimeJoyPlayerParseProvider(
            AnimeJoySiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.ProForAny;

        public bool CanOpenFromLinkOrHostingName(Uri link)
        {
            var rootDomain = link.GetRootDomain();

            return QueryStringHelper.ParseQuery(link?.Query).Any(p => p.Key == "file")
                && siteProvider.Mirrors.Any(m => m.GetRootDomain() == rootDomain);
        }

        public Task<File?> ParseFromUriAsync(Uri httpUri, CancellationToken cancellationToken)
        {
            var filePath = QueryStringHelper.ParseQuery(httpUri.Query).First(p => p.Key == "file").Value;
            var videos = ProviderHelper.ParseVideosFromPlayerJsString(filePath, httpUri);

            // Warning, Unstable ID.
            var file = new File(Site, "ajoy" + filePath.GetDeterministicHashCode());
            file.FrameLink = httpUri;
            file.SetVideos(videos.Select(t => t.video).ToArray());

            return Task.FromResult<File?>(file);
        }
    }
}

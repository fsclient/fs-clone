namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using YoutubeExplode;
    using YoutubeExplode.Common;
    using YoutubeExplode.Videos;
    using YoutubeExplode.Videos.Streams;

    /// <inheritdoc/>
    public class YoutubePlayerParseProvider : IPlayerParseProvider
    {
        private readonly YoutubeSiteProvider siteProvider;
        private readonly ILogger logger;

        public YoutubePlayerParseProvider(
            YoutubeSiteProvider siteProvider,
            ILogger logger)
        {
            this.siteProvider = siteProvider;
            this.logger = logger;
        }

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        /// <inheritdoc/>
        public bool CanOpenFromLinkOrHostingName(Uri link)
        {
            return VideoId.TryParse(link.ToString()).HasValue;
        }

        /// <inheritdoc/>
        public async Task<File?> ParseFromUriAsync(Uri link, CancellationToken cancellationToken)
        {
            try
            {
                if (VideoId.TryParse(link.ToString())?.Value is not string videoId)
                {
                    return null;
                }

                var client = new YoutubeClient(siteProvider.HttpClient);
                var video = await client.Videos.GetAsync(videoId, cancellationToken).ConfigureAwait(false);
                var ytVideos = await client.Videos.Streams.GetManifestAsync(videoId, cancellationToken).ConfigureAwait(false);

                var videos = ytVideos.GetMuxedStreams()
                    .Where(v => v.Container == Container.Mp4 || v.Container == Container.Tgpp)
                    .GroupBy(v => v.VideoQuality)
                    .Select(g =>
                    {
                        var v = g.First();

                        return new Shared.Models.Video(new Uri(v.Url))
                        {
                            Quality = v.VideoQuality.Label
                        };
                    })
                    .Where(v => v.SingleLink?.IsAbsoluteUri == true)
                    .ToArray();

                var file = new File(Site, "yt" + videoId);
                file.Title = video.Title;
                file.FrameLink = link;

                if (video.Thumbnails.TryGetWithHighestResolution() is Thumbnail highestQuality)
                {
                    file.PlaceholderImage = new Uri(highestQuality.Url);
                }

                file.SetVideos(videos);
                return file;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
            }

            return null;
        }
    }
}

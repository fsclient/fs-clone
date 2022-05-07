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
    public class PlayerJsAbstractPlayerParseProvider : IPlayerParseProvider
    {
        private readonly PlayerJsAbstractSiteProvider siteProvider;
        private readonly PlayerJsParserService playerJsParserService;

        public PlayerJsAbstractPlayerParseProvider(
            PlayerJsAbstractSiteProvider siteProvider,
            PlayerJsParserService playerJsParserService)
        {
            this.siteProvider = siteProvider;
            this.playerJsParserService = playerJsParserService;
        }

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLinkOrHostingName(Uri link)
        {
            return FindConfigByHostName(link).key != null;
        }

        public async Task<File?> ParseFromUriAsync(Uri httpUri, CancellationToken cancellationToken)
        {
            var (key, config) = FindConfigByHostName(httpUri);
            if (key == null || config == null)
            {
                return null;
            }

            var pageText = await siteProvider.HttpClient
                .GetBuilder(httpUri)
                .WithHeader("Origin", httpUri.GetOrigin())
                .WithHeader("Referer", httpUri.ToString())
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);
            if (pageText == null)
            {
                return null;
            }

            var videos = await Regex
                .Matches(pageText, @"(?:""|')?file(?:""|')?\s*:\s*(?:""|')(?<file>.*?)(?:""|')")
                .OfType<Match>()
                .Where(m => m.Groups["file"].Success)
                .ToAsyncEnumerable()
                .Select(m => m.Groups["file"].Value)
                .WhenAll((file, ct) => playerJsParserService.DecodeAsync(file, config.PlayerJsConfig ?? siteProvider.PlayerJsConfig, ct).AsTask())
                .Where(decoded => decoded != null)
                .SelectMany(decoded => ProviderHelper.ParseVideosFromPlayerJsString(decoded!, httpUri).Select(t => t.video).ToAsyncEnumerable())
                .DistinctBy(v => v.Quality)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            var fileId = config.NodeIdPrefix + Regex.Match(httpUri.GetPath(), config.IdRegex).Groups["id"].Value;
            var file = new File(Site.Parse(key, default, true), fileId);
            file.FrameLink = httpUri;
            file.SetVideos(videos);

            return file;
        }

        private (string key, PlayerJsAbstractSiteConfig config) FindConfigByHostName(Uri link)
        {
            var hosting = link.GetRootDomain().GetLettersAndDigits().ToLower();

            return siteProvider.SupportedWebSites
                .Select(p => (p.Key, p.Value))
                .FirstOrDefault(t => t.Value.AllowedMirrors.Any(m => m.GetRootDomain().GetLettersAndDigits().StartsWith(hosting, StringComparison.OrdinalIgnoreCase)));
        }
    }
}

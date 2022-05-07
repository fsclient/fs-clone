namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    /// <inheritdoc cref="IPlayerParseManager" />
    public sealed class PlayerParseManager : IPlayerParseManager, IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly IPlayerParseProvider[] parseProviders;
        private readonly IUserManager userManager;
        private readonly ILogger logger;

        public PlayerParseManager(
            IEnumerable<IPlayerParseProvider> parseProviders,
            IUserManager userManager,
            ILogger logger)
        {
            this.parseProviders = parseProviders.ToArray();
            this.userManager = userManager;
            this.logger = logger;
            httpClient = new HttpClient();
        }

        public bool CanOpenFromLinkOrHostingName(Uri httpUri, Site knownSite)
        {
            return httpUri != null && (httpUri.IsAbsoluteUri || knownSite != default)
                && parseProviders.Any(provider => provider.Site == knownSite || provider.CanOpenFromLinkOrHostingName(httpUri));
        }

        public async Task<File?> ParseFromUriAsync(Uri httpUri, Site knownSite, CancellationToken cancellationToken)
        {
            try
            {
                if (httpUri == null)
                {
                    return null;
                }
                var availableProvider = parseProviders.FirstOrDefault(provider =>
                    provider.Site == knownSite || provider.CanOpenFromLinkOrHostingName(httpUri));
                var file = availableProvider != null
                    ? await GetVideosFromSpecificProvider(availableProvider, httpUri, cancellationToken).ConfigureAwait(false)
                    : await GetVideosFromElseAsync(httpUri, cancellationToken).ConfigureAwait(false);

                if (file != null)
                {
                    await file.PreloadAsync(cancellationToken).ConfigureAwait(false);
                    file.Title ??= string.Format(Strings.Player_FileFromSpecificSite, file.Site.Title);
                }

                return file;
            }
            catch (Exception ex)
            {
                ex.Data["Link"] = httpUri;
                logger?.LogError(ex);

                return null;
            }
        }

        private async Task<File?> GetVideosFromSpecificProvider(IPlayerParseProvider provider, Uri httpUri, CancellationToken cancellationToken)
        {
            var isAllowed = await userManager.CheckRequirementsAsync(provider.Site, provider.ReadRequirements, cancellationToken).ConfigureAwait(false);
            if (!isAllowed)
            {
                return null;
            }

            return await provider.ParseFromUriAsync(httpUri, cancellationToken).ConfigureAwait(false);
        }

        private async Task<File?> GetVideosFromElseAsync(Uri httpUri, CancellationToken cancellationToken)
        {
            var resp = await httpClient.GetBuilder(httpUri)
                .SendAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (resp?.Content?.Headers?.ContentType?.MediaType is not string mediaType)
            {
                return null;
            }

            if (mediaType.Contains("video")
                || mediaType.Contains("mpegurl")
                || mediaType.Contains("octet-stream"))
            {
                var file = new File(Site.Any, string.Empty)
                {
                    Title = httpUri.Host
                };
                file.SetVideos(new Video(httpUri));
                return file;
            }

            if (!mediaType.Contains("html"))
            {
                return null;
            }

            var html = await httpClient.GetBuilder(httpUri).SendAsync(cancellationToken).AsHtml(cancellationToken).ConfigureAwait(false);
            if (html == null)
            {
                return null;
            }

            var videoTag = html
                .QuerySelector("video[src], video source[src]");
            var fileSrc = videoTag?.GetAttribute("src");

            if (Uri.TryCreate(httpUri, fileSrc, out var src))
            {
                var file = new File(Site.Any, string.Empty)
                {
                    Title = httpUri.Host
                };
                file.SetVideos(new Video(src));
                return file;
            }

            var iframeTag = html
                .QuerySelector("iframe[src]");
            var iframeSrc = iframeTag?.GetAttribute("src");

            if (Uri.TryCreate(httpUri, iframeSrc, out src)
                && parseProviders.FirstOrDefault(provider => provider
                    .CanOpenFromLinkOrHostingName(src)) is IPlayerParseProvider provider)
            {
                return await GetVideosFromSpecificProvider(provider, src, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}

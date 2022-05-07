namespace FSClient.Shared.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;

    public abstract class BaseSiteProvider : IHttpSiteProvider
    {
        protected static readonly HttpClientHandler DefaultHandler;
        private const string EncodedPrefixScheme = "encoded";

        static BaseSiteProvider()
        {
            DefaultHandler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AllowAutoRedirect = false
            };
            if (DefaultHandler.SupportsAutomaticDecompression)
            {
                DefaultHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
        }

        private readonly IProviderConfigService providerConfigService;
        private readonly Dictionary<string, string?> properties;

        protected BaseSiteProvider(
            IProviderConfigService providerConfigService,
            ProviderConfig defaultProviderConfig)
        {
            this.providerConfigService = providerConfigService;
            Site = defaultProviderConfig.Site;

            var config = providerConfigService.GetConfig(defaultProviderConfig.Site);

            Details = new ProviderDetails(
                config.Requirements ?? defaultProviderConfig.Requirements ?? ProviderRequirements.None,
                config.Priority ?? defaultProviderConfig.Priority ?? 0);

            var mirrors = config.Mirrors ?? defaultProviderConfig.Mirrors
                ?? throw new InvalidOperationException("Mirrors array must not be empty");
            if (providerConfigService.GetUserMirror(Site) is Uri userMirror)
            {
                mirrors = new[] { userMirror }.Concat(mirrors);
            }
            Mirrors = mirrors
                .Select(l => l.Scheme == EncodedPrefixScheme
                    ? new Uri(ProcessPropertyValue(l.OriginalString))
                    : l)
                .ToList();

            HealthCheckRelativeLink = config.HealthCheckRelativeLink ?? defaultProviderConfig.HealthCheckRelativeLink;
            CanBeMain = config.CanBeMain ?? defaultProviderConfig.CanBeMain ?? false;
            IsVisibleToUser = config.IsVisibleToUser ?? defaultProviderConfig.IsVisibleToUser ?? true;
            EnforceDisabled = (config.EnforceDisabled ?? false) || (defaultProviderConfig.EnforceDisabled ?? false);
            IsEnabledByDefault = config.IsEnabledByDefault ?? defaultProviderConfig.IsEnabledByDefault ?? true;
            MirrorCheckingStrategy = config.MirrorCheckingStrategy ?? defaultProviderConfig.MirrorCheckingStrategy ?? ProviderMirrorCheckingStrategy.Parallel;

            properties = (config.Properties?.AsEnumerable() ?? Enumerable.Empty<KeyValuePair<string, string?>>())
                .Concat(defaultProviderConfig.Properties?.AsEnumerable() ?? Enumerable.Empty<KeyValuePair<string, string?>>())
                .GroupBy(p => p.Key)
                .ToDictionary(p => p.Key, p => ProcessPropertyValue(p.First().Value));

            Handler = DefaultHandler;
            HttpClient = new HttpClient(Handler);
        }

        public Site Site { get; }

        public virtual HttpClient HttpClient { get; protected set; }
        public virtual HttpClientHandler Handler { get; protected set; }

        public ProviderDetails Details { get; }

        public IReadOnlyList<Uri> Mirrors { get; }

        public IReadOnlyDictionary<string, string?> Properties => properties;

        public bool CanBeMain { get; }

        public bool IsVisibleToUser { get; }

        public bool EnforceDisabled { get; }

        public bool IsEnabledByDefault { get; }

        public Uri? HealthCheckRelativeLink { get; }

        protected ProviderMirrorCheckingStrategy MirrorCheckingStrategy { get; }

        public User? CurrentUser { get; set; }

        public virtual ITimeSpanSemaphore RequestSemaphore => TimeSpanSemaphore.Empty;

        public virtual ValueTask<ItemInfo> EnsureItemAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var mirrorTask = GetMirrorAsync(cancellationToken);
            if (mirrorTask.IsCompleted)
            {
                EnsureItemInternal(mirrorTask.Result);
                return new ValueTask<ItemInfo>(itemInfo);
            }

            return new ValueTask<ItemInfo>(EnsureItemInternalAsync());

            async Task<ItemInfo> EnsureItemInternalAsync()
            {
                var mirror = await mirrorTask.ConfigureAwait(false);
                EnsureItemInternal(mirror);
                return itemInfo;
            }

            void EnsureItemInternal(Uri mirror)
            {
                itemInfo.Link = EnsureItemLink(itemInfo.Link, itemInfo.SiteId, mirror);
                itemInfo.Poster = EnsureItemImage(itemInfo.Poster, mirror);
            }
        }

        public virtual async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            var mirror = await GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            if (HealthCheckRelativeLink is Uri relativeLink)
            {
                mirror = new Uri(mirror, relativeLink);
            }

            var mirrorCheckConfig = PrepareMirrorGetterConfig();

            var (_, _, isAvailable) = await mirror
                .IsAvailableWithLocationAsync(
                    mirrorCheckConfig.HttpMethod, mirrorCheckConfig.AdditionalHeaders, IsValidMirrorResponse, cancellationToken, RequestSemaphore)
                .ConfigureAwait(false);

            return isAvailable;
        }

        public ValueTask<Uri> GetMirrorAsync(CancellationToken cancellationToken)
        {
            var config = PrepareMirrorGetterConfig();
            return providerConfigService.GetMirrorAsync(Site, config, cancellationToken);
        }

        protected virtual MirrorGetterConfig PrepareMirrorGetterConfig()
        {
            return new MirrorGetterConfig(Mirrors)
            {
                Handler = Handler,
                MirrorCheckingStrategy = MirrorCheckingStrategy,
                HealthCheckRelativeLink = HealthCheckRelativeLink,
                Validator = IsValidMirrorResponse
            };
        }

        protected virtual Uri? EnsureItemLink(Uri? itemLink, string? id, Uri mirror)
        {
            if (itemLink == null)
            {
                return null;
            }
            if (!itemLink.IsAbsoluteUri)
            {
                return new Uri(mirror, itemLink);
            }
            else if (itemLink.Host != mirror.Host)
            {
                var builder = new UriBuilder(itemLink);
                builder.Host = mirror.Host;
                return builder.Uri;
            }
            return itemLink;
        }

        protected virtual WebImage EnsureItemImage(WebImage image, Uri mirror)
        {
            if (image.Count == 1
                && image[ImageSize.Preview] is Uri previewPoster
                && !previewPoster.IsAbsoluteUri)
            {
                return new WebImage
                {
                    [ImageSize.Preview] = new Uri(mirror, previewPoster)
                };
            }
            return image;
        }

        protected virtual ValueTask<bool> IsValidMirrorResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            return new ValueTask<bool>(responseMessage.IsSuccessStatusCode);
        }

        private static string? ProcessPropertyValue(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            if (input!.Length <= EncodedPrefixScheme.Length + 1)
            {
                return input;
            }

            if (input.StartsWith(EncodedPrefixScheme, StringComparison.OrdinalIgnoreCase))
            {
                return AesEncryption
                    .Decrypt(
                        Convert.FromBase64String(input.Substring(EncodedPrefixScheme.Length + 1).Replace('_', '/').Replace('-', '+')),
                        Convert.FromBase64String(Secrets.AppInternalKey!),
                        Convert.FromBase64String(Secrets.AppInternalIV!));
            }

            return input;
        }
    }
}

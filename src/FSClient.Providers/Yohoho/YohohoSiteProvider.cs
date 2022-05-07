namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class YohohoSiteProvider : BaseSiteProvider
    {
        private static readonly ITimeSpanSemaphore requestSemaphore =
            TimeSpanSemaphore.Create(1, TimeSpan.FromMilliseconds(100));

        internal const string YohohoRefererKey = "YohohoReferer";

        public YohohoSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Yohoho,
                isEnabledByDefault: false,
                healthCheckRelativeLink: new Uri("/4h0y", UriKind.Relative),
                properties: new Dictionary<string, string?>
                {
                    [YohohoRefererKey] = "http://4h0y.gitlab.io/webmaster.html"
                },
                mirrors: new[] { new Uri("https://ahoy.yohoho.online") }))
        {
        }

        public override ITimeSpanSemaphore RequestSemaphore => requestSemaphore;

        protected override MirrorGetterConfig PrepareMirrorGetterConfig()
        {
            var referer = new Uri(Properties[YohohoRefererKey], UriKind.Absolute);

            return base.PrepareMirrorGetterConfig() with
            {
                HttpMethod = HttpMethod.Get,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Origin"] = referer.GetOrigin() ?? string.Empty,
                    ["Referer"] = referer.ToString(),
                }
            };
        }

        protected override async ValueTask<bool> IsValidMirrorResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            return await base.IsValidMirrorResponse(responseMessage, cancellationToken)
                || responseMessage.Headers?.Location?.Host == "127.0.0.1";
        }
    }
}

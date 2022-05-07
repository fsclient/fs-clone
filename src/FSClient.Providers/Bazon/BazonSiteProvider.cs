namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared;
    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Services;

    public class BazonSiteProvider : BasePlayerJsSiteProvider
    {
        private static readonly ITimeSpanSemaphore requestSemaphore =
            TimeSpanSemaphore.Create(1, TimeSpan.FromMilliseconds(500));

        internal const string BazonRefererKey = "BazonReferer";
        internal const string BazonDecodeKeyKey = "BazonDecodeKey";
        internal const string BazonPathKeyKey = "BazonPathKey";
        internal const string BazonMitmScriptLinkKey = "BazonMitmScriptLink";
        internal const string BazonApiLinkKey = "UStoreApiLink";
        internal const string BazonForceYohohoSearchKey = "UStoreForceYohohoSearch";

        public BazonSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Bazon,
                enforceDisabled: string.IsNullOrEmpty(Secrets.BazonApiKey),
                properties: new Dictionary<string, string?>
                {
                    [PlayerJsConfigKey] = "{\"Keys\":[\"ZTUl\", \"ZDQk\", \"YzMj\", \"YjJA\", \"YTEh\"],\"Separator\":\"@\",\"OyKey\":\"xx???x=xx?xxx=\"}",
                    [BazonDecodeKeyKey] = "KX7tX2r28y",
                    [BazonPathKeyKey] = "@",
                    [BazonForceYohohoSearchKey] = bool.TrueString,
                    [BazonApiLinkKey] = "https://bazon.cc",
                    [BazonRefererKey] = "https://4h0y.bitbucket.io/" // set 'null' to use Host as Referer
                },
                mirrors: new[] { new Uri("http://yo.bazon.site") }))
        {
        }

        public override ITimeSpanSemaphore RequestSemaphore => requestSemaphore;
    }
}

namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class UStoreSiteProvider : BaseSiteProvider
    {
        private static readonly ITimeSpanSemaphore requestSemaphore =
            TimeSpanSemaphore.Create(1, TimeSpan.FromMilliseconds(100));

        internal const string UStoreRefererKey = "UStoreReferer";

        internal const string UStoreMitmScriptLinkKey = "UStoreMitmScriptLink";
        internal const string UStorePlayerLinkKey = "UStorePlayerLink";
        internal const string UStoreSecurityKeyKey = "UStoreSecurityKey";
        internal const string UStoreSecurityKey2Key = "UStoreSecurity2Key";
        internal const string UStoreApiLinkKey = "UStoreApiLink";
        internal const string UStoreVideoDetailsLinkKey = "UStoreVideoDetailsLink";
        internal const string UStoreForceYohohoSearchKey = "UStoreForceYohohoSearch";

        public UStoreSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.UStore,
                // enforceDisabled: string.IsNullOrEmpty(Secrets.UStoreApiKey),
                properties: new Dictionary<string, string?>
                {
                    [UStoreSecurityKey2Key] = "[\"WO87FXYEZP4abQ2cdR0efS9ghTHijUK\", \"k6lBmCnJoMpGq3rAsLt1uNv5wDxIyVz\"]",
                    [UStorePlayerLinkKey] = "/player/uplayer.js",
                    [UStoreForceYohohoSearchKey] = bool.TrueString,
                    [UStoreApiLinkKey] = "https://apidevel.ustore.bz",
                    [UStoreRefererKey] = "https://hdvbplayer.pw" // set 'null' to use Host as Referer
                },
                mirrors: new[] { new Uri("https://get.u-stream.in") }))
        {
        }

        public override ITimeSpanSemaphore RequestSemaphore => requestSemaphore;
    }
}

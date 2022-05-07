namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Services;

    using Newtonsoft.Json;

    public class PlayerJsAbstractSiteProvider : BasePlayerJsSiteProvider
    {
        public const string SupportedWebSitesKey = "SupportedWebSites";
        private readonly Dictionary<string, PlayerJsAbstractSiteConfig> webSites;

        public PlayerJsAbstractSiteProvider(IProviderConfigService providerConfigService) : base(
           providerConfigService,
           new ProviderConfig(Sites.PlayerJs,
               isVisibleToUser: false,
               properties: new Dictionary<string, string?>
               {
                   [SupportedWebSitesKey] = "{}"
               },
               mirrors: new[] { new Uri("https://playerjs.com") }))
        {
            webSites = new Dictionary<string, PlayerJsAbstractSiteConfig>
            {
                //[Sites.Stormo.Value] = new PlayerJsAbstractSiteConfig("strmo", @"embed\/(?<id>\d+)\/", new[] { new Uri("https://stormo.online") }),
                [Sites.Mediatoday.Value] = new PlayerJsAbstractSiteConfig("mtoday", @"embed\/(?<id>\d+)\/", new[] { new Uri("https://mediatoday.ru") }),
                //[Sites.ProtonVideo.Value] = new PlayerJsAbstractSiteConfig("proton", @"iframe\/(?<id>[^\/]+)\/", new[] { new Uri("https://protonvideo.to") }),
                [Sites.Fsst.Value] = new PlayerJsAbstractSiteConfig("csst", @"embed\/(?<id>\d+)", new[] { new Uri("https://secvideo1.online"), new Uri("https://fsst.online"), new Uri("https://csst.online") }),
                //[Sites.IoUa.Value] = new PlayerJsAbstractSiteConfig("io", @"\/?(?<id>\d+\.\d+\.\d+)", new[] { new Uri("https://io.ua") }),
                [Sites.Tortuga.Value] = new PlayerJsAbstractSiteConfig("ttg", @"vod\/(?<id>\d+)", new[] { new Uri("https://tortuga.wtf") }),
                [Sites.Ashdi.Value] = new PlayerJsAbstractSiteConfig("ahd", @"vod\/(?<id>\d+)", new[] { new Uri("https://ashdi.vip") }),
            };

            if (Properties.TryGetValue(SupportedWebSitesKey, out var supportedWebSitesStr)
                && supportedWebSitesStr != null)
            {
                var supportedWebSitePairs = JsonConvert.DeserializeObject<Dictionary<string, PlayerJsAbstractSiteConfig>>(supportedWebSitesStr);
                if (supportedWebSitePairs != null)
                {
                    foreach (var pair in supportedWebSitePairs)
                    {
                        webSites[pair.Key] = pair.Value;
                    }
                }
            }
        }

        public IReadOnlyDictionary<string, PlayerJsAbstractSiteConfig> SupportedWebSites => webSites;
    }
}

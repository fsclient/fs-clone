namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;

    public class UASerialsSiteProvider : BasePlayerJsSiteProvider
    {
        internal const string UASerialsPassphraseKey = "UASerialsPassphrase";

        public UASerialsSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.UASerials,
                isEnabledByDefault: ProviderHelper.ShouldUkrainianProvidersBeEnabledByDefault,
                properties: new Dictionary<string, string?>
                {
                    [UASerialsPassphraseKey] = "297796CCB81D2551"
                },
                mirrors: new[]
                {
                    new Uri("https://uaserials.pro")
                }))
        {
        }
    }
}

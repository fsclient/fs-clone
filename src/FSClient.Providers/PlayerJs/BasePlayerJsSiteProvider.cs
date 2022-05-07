namespace FSClient.Providers
{
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Newtonsoft.Json;

    public abstract class BasePlayerJsSiteProvider : BaseSiteProvider
    {
        protected const string PlayerJsConfigKey = nameof(PlayerJsConfig);

        protected BasePlayerJsSiteProvider(
            IProviderConfigService providerConfigService,
            ProviderConfig providerConfig)
            : base(providerConfigService, providerConfig)
        {
            if (Properties.TryGetValue(PlayerJsConfigKey, out var playerJsConfig)
                && playerJsConfig != null)
            {
                PlayerJsConfig = JsonConvert.DeserializeObject<PlayerJsConfig>(playerJsConfig) ?? new PlayerJsConfig();
            }
            else
            {
                PlayerJsConfig ??= new PlayerJsConfig();
            }
        }

        public PlayerJsConfig PlayerJsConfig { get; }
    }
}

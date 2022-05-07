namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class AnitubeSiteProvider : BaseSiteProvider
    {
        private static readonly ITimeSpanSemaphore timeSpanSemaphore = TimeSpanSemaphore.Combine(
            TimeSpanSemaphore.Create(15, TimeSpan.FromMilliseconds(500)),
            TimeSpanSemaphore.Create(100, TimeSpan.FromMinutes(1)));

        public AnitubeSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Anitube,
                isEnabledByDefault: ProviderHelper.ShouldUkrainianProvidersBeEnabledByDefault,
                mirrors: new[] { new Uri("https://anitube.in.ua") }))
        {
        }

        public override ITimeSpanSemaphore RequestSemaphore => timeSpanSemaphore;
    }
}

namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class FindAnimeSiteProvider : BaseSiteProvider
    {
        private static readonly ITimeSpanSemaphore timeSpanSemaphore = TimeSpanSemaphore.Combine(
            TimeSpanSemaphore.Create(15, TimeSpan.FromMilliseconds(500)),
            TimeSpanSemaphore.Create(100, TimeSpan.FromMinutes(1)));

        public FindAnimeSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.FindAnime,
                mirrors: new[] { new Uri("https://findanime.net") }))
        {
        }

        public override ITimeSpanSemaphore RequestSemaphore => timeSpanSemaphore;
    }
}

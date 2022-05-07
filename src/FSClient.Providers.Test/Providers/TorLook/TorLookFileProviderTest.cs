namespace FSClient.Providers.Test.TorLook
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class TorLookFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new TorLookSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("Avengers: Endgame", "Мстители: Финал", 2019)]
        public async Task TorLookFileProvider_GetTorrents(string enTitle, string ruTitle, int year)
        {
            var siteProvider = new TorLookSiteProvider(Test.ProviderConfigService);
            var item = new ItemInfo(siteProvider.Site, "-")
            {
                Title = ruTitle,
                Details =
                {
                    TitleOrigin = enTitle,
                    Year = year
                }
            };

            var provider = new TorLookFileProvider(siteProvider);

            var nodes = await provider.GetTorrentsRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var torrents = ProviderTestHelpers.GetDeepNodes<TorrentFolder>(nodes!).ToList();

            await ProviderTestHelpers
                .CheckTorrentsAsync(
                    torrents,
                    CheckTorrentFlags.UniqueLinks | CheckTorrentFlags.CheckSeeds | CheckTorrentFlags.CheckLeaches)
                .ConfigureAwait(false);
        }
    }
}

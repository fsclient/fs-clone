namespace FSClient.Providers.Test.ThePirateBay
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class ThePirateBayFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ThePirateBaySiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("Avengers: Endgame", 2019)]
        public async Task ThePirateBayFileProvider_GetTorrents(string enTitle, int year)
        {
            var siteProvider = new ThePirateBaySiteProvider(Test.ProviderConfigService);
            var item = new ItemInfo(siteProvider.Site, "-")
            {
                Title = enTitle,
                Details =
                {
                    Year = year
                }
            };

            var provider = new ThePirateBayFileProvider(siteProvider);

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

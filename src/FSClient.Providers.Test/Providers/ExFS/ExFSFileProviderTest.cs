namespace FSClient.Providers.Test.ExFS
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class ExFSFileProviderTest

    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ExFSSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("Босс-молокосос")]
        public async Task ExFsFileProvider_GetTorrents(string title)
        {
            var siteProvider = new ExFSSiteProvider(Test.ProviderConfigService);
            var item = new ItemInfo(siteProvider.Site, "-")
            {
                Title = title
            };

            var provider = new ExFSFileProvider(siteProvider);

            var nodes = await provider.GetTorrentsRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var torrents = ProviderTestHelpers.GetDeepNodes<TorrentFolder>(nodes!).ToList();

            await ProviderTestHelpers
                .CheckTorrentsAsync(
                    torrents,
                    CheckTorrentFlags.UniqueLinks | CheckTorrentFlags.CheckPeers | CheckTorrentFlags.CheckSeeds)
                .ConfigureAwait(false);
        }
    }
}

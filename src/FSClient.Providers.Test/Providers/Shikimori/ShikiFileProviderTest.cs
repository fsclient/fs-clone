namespace FSClient.Providers.Test.Shikimori
{
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class ShikiFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ShikiSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase(31646)]    // 3-gatsu no Lion
        public async Task ShikiFileProvider_GetTrailers(int id)
        {
            var siteProvider = new ShikiSiteProvider(Test.ProviderConfigService);
            var provider = new ShikiFileProvider(siteProvider, null!, new PlayerParseManagerStub());
            var item = new ItemInfo(provider.Site, id.ToString());

            var nodes = await provider.GetTrailersRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var files = ProviderTestHelpers.GetDeepNodes<File>(nodes!);

            await ProviderTestHelpers.CheckFilesAsync(files, CheckFileFlags.Default | CheckFileFlags.Trailers | CheckFileFlags.IgnoreVideos).ConfigureAwait(false);
        }
    }
}

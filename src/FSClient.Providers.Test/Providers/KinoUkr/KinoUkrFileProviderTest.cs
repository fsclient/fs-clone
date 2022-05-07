namespace FSClient.Providers.Test.KinoUkr
{
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class KinoUkrFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new KinoUkrSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase(3270)]   // Hellboy - tortuga
        [TestCase(3695)]   // Adult Wednesday Addams - youtube 
        public async Task KinoUkrFileProvider_GetTrailers(int id)
        {
            var siteProvider = new KinoUkrSiteProvider(Test.ProviderConfigService);
            var provider = new KinoUkrFileProvider(siteProvider, new TortugaFileProvider(new TortugaSiteProvider(Test.ProviderConfigService)), new PlayerParseManagerStub());
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Title = "-"
            };

            var nodes = await provider.GetTrailersRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var files = ProviderTestHelpers.GetDeepNodes<File>(nodes!);

            await ProviderTestHelpers.CheckFilesAsync(files, CheckFileFlags.Default | CheckFileFlags.Trailers | CheckFileFlags.IgnoreVideos).ConfigureAwait(false);
        }

        [Timeout(60000)]
        [TestCase(3834, true)]   // Sex and the City
        [TestCase(3817, false)]  // The Lion King 2019
        public async Task KinoUkrFileProvider_LoadFolder_Full_Tortuga(int id, bool isSerial)
        {
            var siteProvider = new KinoUkrSiteProvider(Test.ProviderConfigService);
            var provider = new KinoUkrFileProvider(siteProvider, new TortugaFileProvider(new TortugaSiteProvider(Test.ProviderConfigService)), new PlayerParseManagerStub());
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Title = "-"
            };

            provider.InitForItems(new[] { item });
            var folder = new Folder(provider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);

            var files = await ProviderTestHelpers.PreloadAndGetFirstOfType<File>(provider, folder).ConfigureAwait(false);

            await ProviderTestHelpers
                .CheckFilesAsync(files, isSerial
                    ? CheckFileFlags.Default | CheckFileFlags.IgnoreVideos | CheckFileFlags.Serial
                    : CheckFileFlags.Default | CheckFileFlags.IgnoreVideos)
                .ConfigureAwait(false);
        }
    }
}

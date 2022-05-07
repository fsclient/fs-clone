namespace FSClient.Providers.Test.Bazon
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    [Ignore("Broken")]
    public class BazonFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new BazonSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(60000)]
        [TestCase("/embed/170a23f16e1fb815f11ea56e0f0844f9", false)]   // The Fast and the Furious
        [TestCase("/embed/edfc6ccbcd1745f7b91fd51d3620487f", true)]  // Friends
        public async Task BazonFileProvider_LoadFolder_Full(string link, bool isSerial)
        {
            var item = new ItemInfo(Site.Any, "-")
            {
                Title = "ignore",
                Link = new Uri(link, UriKind.Relative)
            };

            var siteProvider = new BazonSiteProvider(Test.ProviderConfigService);
            var provider = new BazonFileProvider(siteProvider, new PlayerJsParserService(Test.SettingService, Test.Logger), Test.Logger);

            provider.InitForItems(new[] { item });
            var folder = new Folder(siteProvider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);

            var files = await ProviderTestHelpers.PreloadAndGetDeepNodes<File>(provider, folder).ConfigureAwait(false);

            await ProviderTestHelpers
                .CheckFilesAsync(files, isSerial
                    ? CheckFileFlags.UniqueIDs | CheckFileFlags.AtLeastOne | CheckFileFlags.Serial
                    : CheckFileFlags.UniqueIDs | CheckFileFlags.AtLeastOne)
                .ConfigureAwait(false);
        }
    }
}

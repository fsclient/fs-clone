namespace FSClient.Providers.Test.UASerials
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class UASerialsFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new UASerialsSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("/1548-chorni-kadry.html")]
        [TestCase("/794-star-proti-sil-zla-sezon-1.html")]
        [Timeout(60000)]
        public async Task UASerials_InitFromLink_And_LoadFull(string link)
        {
            var siteProvider = new UASerialsSiteProvider(Test.ProviderConfigService);
            var provider = new UASerialsItemInfoProvider(siteProvider);
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            Assert.That(item, Is.Not.Null, "Item is null");

            var fileProvider = new UASerialsFileProvider(siteProvider, new TortugaFileProvider(new TortugaSiteProvider(Test.ProviderConfigService)), Test.Logger);

            fileProvider.InitForItems(new[] { item }!);
            var folder = new Folder(siteProvider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);

            var files = await ProviderTestHelpers.PreloadAndGetDeepNodes<File>(fileProvider, folder).ConfigureAwait(false);

            await ProviderTestHelpers
                .CheckFilesAsync(files, CheckFileFlags.UniqueIDs
                    | CheckFileFlags.AtLeastOne
                    | CheckFileFlags.Serial)
                .ConfigureAwait(false);
        }
    }
}

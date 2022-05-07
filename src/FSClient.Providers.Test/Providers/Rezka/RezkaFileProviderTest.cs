namespace FSClient.Providers.Test.Rezka
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class RezkaFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new RezkaSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase(2407)]     // The Hunger Games: Mockingjay - Part 1
        [TestCase(31756)]    // Downton Abbey
        public async Task RezkaFileProvider_GetTrailers(int id)
        {
            var siteProvider = new RezkaSiteProvider(Test.ProviderConfigService);
            var provider = new RezkaFileProvider(siteProvider, new PlayerParseManagerStub());
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Link = new Uri($"/series/thriller/{id}-item.html", UriKind.Relative)
            };

            var nodes = await provider.GetTrailersRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var files = ProviderTestHelpers.GetDeepNodes<File>(nodes!);

            await ProviderTestHelpers.CheckFilesAsync(files, CheckFileFlags.Default | CheckFileFlags.Trailers | CheckFileFlags.IgnoreVideos).ConfigureAwait(false);
        }

        [TestCase("/series/drama/1846-abbatstvo-daunton-2010.html", true)]
        [TestCase("/films/thriller/32292-dzhoker-2019.html", false)]
        [TestCase("/films/comedy/36707-rodnye-2021.html", false)]
        [TestCase("/series/adventures/32545-avatar-korolya-2019.html", true)]
        [Timeout(60000)]
        public async Task Rezka_OpenFromLink_Full(string link, bool isSerial)
        {
            var siteProvider = new RezkaSiteProvider(Test.ProviderConfigService);
            var itemInfoProvidier = new RezkaItemInfoProvider(siteProvider);
            var fileProvider = new RezkaFileProvider(siteProvider, null!);

            var item = await itemInfoProvidier.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            Assert.That(item, Is.Not.Null, "Item is null");

            fileProvider.InitForItems(new[] { item }!);
            var folder = new Folder(siteProvider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);
            var result = await fileProvider.GetFolderChildrenAsync(folder, CancellationToken.None).ConfigureAwait(false);
            folder.AddRange(result);

            Assert.That(result.Any(), Is.True, "Folder items source is empty");

            var files = await ProviderTestHelpers.PreloadAndGetDeepNodes<File>(fileProvider, folder).ConfigureAwait(false);

            await ProviderTestHelpers
                .CheckFilesAsync(files, isSerial
                    ? CheckFileFlags.UniqueIDs | CheckFileFlags.AtLeastOne | CheckFileFlags.Serial
                    : CheckFileFlags.UniqueIDs | CheckFileFlags.AtLeastOne)
                .ConfigureAwait(false);
        }
    }
}

namespace FSClient.Providers.Test.UStore
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    [Ignore("Broken")]
    public class UStoreFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new UStoreSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(60000)]
        [TestCase("/start/4c384192c35245c7d527bc9845673237/e924517087669cf201ea91bd737a4ff4", false)]   // The Fast and the Furious
        [TestCase("/start/4c384192c35245c7d527bc9845673237/ec0bfd000f253eff3acb1043e1c06979", true)]    // Arrow
        public async Task UStoreFileProvider_LoadFolder_Full(string link, bool isSerial)
        {
            var item = new ItemInfo(Site.Any, "-")
            {
                Title = "ignore",
                Link = new Uri(link, UriKind.Relative)
            };

            var siteProvider = new UStoreSiteProvider(Test.ProviderConfigService);
            var provider = new UStoreFileProvider(siteProvider, Test.Logger);

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

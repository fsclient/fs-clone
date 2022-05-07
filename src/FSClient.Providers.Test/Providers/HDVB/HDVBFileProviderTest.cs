namespace FSClient.Providers.Test.HDVB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class HDVBFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new HDVBSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(60000)]
        [TestCase(666, false)]
        [TestCase(404900, true)]
        public async Task HDVBFileProvider_LoadFolder_Full(int kpId, bool isSerial)
        {
            var item = new ItemInfo(Site.Any, "-")
            {
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film),
                Details =
                {
                    LinkedIds =
                    {
                        [Sites.Kinopoisk] = kpId.ToString()
                    }
                }
            };

            var siteProvider = new HDVBSiteProvider(Test.ProviderConfigService);
            var searchProvider = new HDVBSearchProvider(siteProvider);
            var items = await searchProvider.FindSimilarAsync(item, CancellationToken.None).ConfigureAwait(false);
            var provider = new HDVBFileProvider(siteProvider);

            provider.InitForItems(items);
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

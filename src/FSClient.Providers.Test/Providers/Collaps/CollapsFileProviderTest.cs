namespace FSClient.Providers.Test.Collaps
{
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    [Ignore("Broken")]
    public class CollapsFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new CollapsSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(60000)]
        [TestCase(444, false)]       // Terminator 2: Judgment Day
        [TestCase(1289686, true)]    // The King's Avatar
        public async Task CollapsFileProvider_LoadFolder_Full(int kpId, bool isSerial)
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

            var siteProvider = new CollapsSiteProvider(Test.ProviderConfigService);
            var searchProvider = new CollapsSearchProvider(siteProvider);
            var items = await searchProvider.FindSimilarAsync(item, CancellationToken.None).ConfigureAwait(false);
            var provider = new CollapsFileProvider(siteProvider);

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

namespace FSClient.Providers.Test.Lookbase
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class LookbaseFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new LookbaseSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(60000)]
        [TestCase("/sc/m23-Otk/m23-pnC0/", false)]
        [TestCase("/sc/m23-DoMf5P/m23-pnp/", false)]
        public async Task LookbaseFileProvider_LoadFolder_Full(string link, bool isSerial)
        {
            var item = new ItemInfo(Site.Any, "-")
            {
                Link = new Uri(link, UriKind.Relative),
                Title = "title",
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film),
            };

            var siteProvider = new LookbaseSiteProvider(Test.ProviderConfigService);
            var provider = new LookbaseFileProvider(siteProvider, new PlayerJsParserService(Test.SettingService, Test.Logger));
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

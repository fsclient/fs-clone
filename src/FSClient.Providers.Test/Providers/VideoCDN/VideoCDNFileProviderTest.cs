namespace FSClient.Providers.Test.VideoCDN
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class VideoCDNFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new VideoCDNSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(60000)]
        [TestCase("/yIE0kdbKybog/tv-series/5", true)]
        [TestCase("/yIE0kdbKybog/movie/32424", false)]
        public async Task VideoCDNFileProvider_LoadFolder_Full(string link, bool isSerial)
        {
            var item = new ItemInfo(Site.Any, "-")
            {
                Link = new Uri(link, UriKind.Relative),
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film),
            };

            var siteProvider = new VideoCDNSiteProvider(Test.ProviderConfigService);
            var provider = new VideoCDNFileProvider(siteProvider, new PlayerJsParserService(Test.SettingService, Test.Logger));
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

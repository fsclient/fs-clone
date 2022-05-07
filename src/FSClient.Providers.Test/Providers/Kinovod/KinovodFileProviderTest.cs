namespace FSClient.Providers.Test.Kinovod
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class KinovodFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new KinovodSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(60000)]
        [TestCase(124697, "/film/124697-moj-sozdatel", false, false)]
        [TestCase(127164, "/serial/127164-112263", true, true)]
        public async Task KinovodFileProvider_LoadFolder_Full(int id, string link, bool isSerial, bool subtitle)
        {
            var siteProvider = new KinovodSiteProvider(Test.ProviderConfigService);
            var provider = new KinovodFileProvider(siteProvider, new PlayerJsParserService(Test.SettingService, Test.Logger), new PlayerParseManagerStub());
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Link = new Uri(link, UriKind.Relative)
            };

            provider.InitForItems(new[] { item });
            var folder = new Folder(provider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);

            var files = await ProviderTestHelpers.PreloadAndGetDeepNodes<File>(provider, folder).ConfigureAwait(false);

            await ProviderTestHelpers
                .CheckFilesAsync(files,
                    (isSerial ? CheckFileFlags.Default | CheckFileFlags.Serial : CheckFileFlags.Default)
                    | (subtitle ? CheckFileFlags.WithSubs : CheckFileFlags.None))
                .ConfigureAwait(false);
        }

        [Timeout(20000)]
        [TestCase(124697, "/film/124697-moj-sozdatel")]
        public async Task KinovodFileProvider_GetTrailers(int id, string link)
        {
            var siteProvider = new KinovodSiteProvider(Test.ProviderConfigService);
            var provider = new KinovodFileProvider(siteProvider, new PlayerJsParserService(Test.SettingService, Test.Logger), new PlayerParseManagerStub());
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Link = new Uri(link, UriKind.Relative)
            };

            var nodes = await provider.GetTrailersRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var files = ProviderTestHelpers.GetDeepNodes<File>(nodes!);

            await ProviderTestHelpers.CheckFilesAsync(files, CheckFileFlags.Default | CheckFileFlags.Trailers | CheckFileFlags.IgnoreVideos).ConfigureAwait(false);
        }
    }
}

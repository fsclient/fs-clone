namespace FSClient.Providers.Test.Filmix
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class FilmixFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new FilmixSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(6 * 60 * 1000)]
        [TestCase(103770, true)]    // Van Helsing
        [TestCase(113456, false)]   // Logan
        public async Task FilmixFileProvider_LoadFolder_Full(int id, bool isSerial)
        {
            var siteProvider = new FilmixSiteProvider(Test.ProviderConfigService);
            var provider = new FilmixFileProvider(siteProvider, new PlayerJsParserService(Test.SettingService, Test.Logger));
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Link = new Uri($"/triller/{id}-item.html", UriKind.Relative)
            };

            provider.InitForItems(new[] { item });
            var folder = new Folder(provider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);

            var files = await ProviderTestHelpers.PreloadAndGetDeepNodes<File>(provider, folder).ConfigureAwait(false);

            await ProviderTestHelpers
                .CheckFilesAsync(files, isSerial
                    ? CheckFileFlags.Default | CheckFileFlags.Serial
                    : CheckFileFlags.Default)
                .ConfigureAwait(false);
        }

        [Timeout(6 * 60 * 1000)]
        [TestCase(95574)]    // Supernatural
        [TestCase(113456)]   // Logan
        public async Task FilmixFileProvider_GetTrailers(int id)
        {
            var siteProvider = new FilmixSiteProvider(Test.ProviderConfigService);
            var provider = new FilmixFileProvider(siteProvider, new PlayerJsParserService(Test.SettingService, Test.Logger));
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Link = new Uri($"/triller/{id}-item.html", UriKind.Relative)
            };

            var nodes = await provider.GetTrailersRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var files = ProviderTestHelpers.GetDeepNodes<File>(nodes!);

            await ProviderTestHelpers.CheckFilesAsync(files, CheckFileFlags.Default | CheckFileFlags.Trailers).ConfigureAwait(false);
        }

        [Timeout(6 * 60 * 1000)]
        [TestCase(117473)]   // 13 Reasons Why
        [TestCase(113456)]   // Logan
        public async Task FilmixFileProvider_GetTorrents(int id)
        {
            var siteProvider = new FilmixSiteProvider(Test.ProviderConfigService);
            var provider = new FilmixFileProvider(siteProvider, null!);
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Link = new Uri($"/triller/{id}-item.html", UriKind.Relative)
            };

            var torrents = await provider.GetTorrentsRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            Assert.That(torrents, Is.Not.Null);
            Assert.That(torrents!.Any(), Is.True, "Zero torrents count");

            var files = ProviderTestHelpers.GetDeepNodes<TorrentFolder>(torrents!).ToList();

            await ProviderTestHelpers.CheckTorrentsAsync(files).ConfigureAwait(false);
        }
    }
}

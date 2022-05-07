namespace FSClient.Providers.Test.Yohoho
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class YohohoFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new YohohoSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("Avengers: Endgame", "Мстители: Финал", 2019)]
        public async Task YohohoFileProvider_GetTorrents(string enTitle, string ruTitle, int year)
        {
            var siteProvider = new YohohoSiteProvider(Test.ProviderConfigService);
            var item = new ItemInfo(siteProvider.Site, "-")
            {
                Title = ruTitle,
                Details =
                {
                    TitleOrigin = enTitle,
                    Year = year
                }
            };

            var provider = new YohohoFileProvider(siteProvider, new YohohoSearchProvider(siteProvider), new PlayerParseManagerStub());

            var nodes = await provider.GetTorrentsRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var torrents = ProviderTestHelpers.GetDeepNodes<TorrentFolder>(nodes!).ToList();

            await ProviderTestHelpers
                .CheckTorrentsAsync(
                    torrents,
                    CheckTorrentFlags.UniqueLinks)
                .ConfigureAwait(false);
        }

        [Timeout(20000)]
        [TestCase("Стекло", 1044601)]
        public async Task YohohoFileProvider_GetTorrents_KpId(string title, int kpId)
        {
            var siteProvider = new YohohoSiteProvider(Test.ProviderConfigService);
            var item = new ItemInfo(siteProvider.Site, $"{kpId}")
            {
                Title = title,
                Details =
                {
                    LinkedIds =
                    {
                        [Sites.Kinopoisk] = kpId.ToString()
                    }
                }
            };

            var provider = new YohohoFileProvider(siteProvider, new YohohoSearchProvider(siteProvider), new PlayerParseManagerStub());

            var nodes = await provider.GetTorrentsRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var torrents = ProviderTestHelpers.GetDeepNodes<TorrentFolder>(nodes!).ToList();

            await ProviderTestHelpers
                .CheckTorrentsAsync(
                    torrents,
                    CheckTorrentFlags.UniqueLinks)
                .ConfigureAwait(false);
        }

        [Timeout(20000)]
        [TestCase("Стекло", 1044601)]
        public async Task YohohoFileProvider_GetTrailers(string title, int kpId)
        {
            var siteProvider = new YohohoSiteProvider(Test.ProviderConfigService);
            var item = new ItemInfo(siteProvider.Site, $"{kpId}")
            {
                Title = title,
                Details =
                {
                    LinkedIds =
                    {
                        [Sites.Kinopoisk] = kpId.ToString()
                    }
                }
            };

            var provider = new YohohoFileProvider(siteProvider, new YohohoSearchProvider(siteProvider), new PlayerParseManagerStub());

            var nodes = await provider.GetTrailersRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var files = ProviderTestHelpers.GetDeepNodes<File>(nodes!).ToList();

            await ProviderTestHelpers
                .CheckFilesAsync(
                    files,
                    CheckFileFlags.Default | CheckFileFlags.Trailers | CheckFileFlags.IgnoreVideos)
                .ConfigureAwait(false);
        }
    }
}

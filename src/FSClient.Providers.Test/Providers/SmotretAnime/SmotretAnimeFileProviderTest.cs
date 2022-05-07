namespace FSClient.Providers.Test.SmotretAnime
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class SmotretAnimeFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return Task.WhenAll(
                new ShikiSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync(),
                new SmotretAnimeSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync());
        }

        [Timeout(30000)]
        [TestCase(31764)]    // Nejimaki Seirei Senki: Tenkyou no Alderamin
        public async Task ShikiSmotretAnimeFileProvider_LoadFolder_Files_From_Shikimori(int shikimoriId)
        {
            var siteProvider = new SmotretAnimeSiteProvider(Test.ProviderConfigService);
            var playerParseProvider = new SmotretAnimePlayerParseProvider(siteProvider);
            var shikiSiteProvider = new ShikiSiteProvider(Test.ProviderConfigService);
            var searchProvider = new SmotretAnimeSearchProvider(siteProvider, shikiSiteProvider);
            var provider = new SmotretAnimeFileProvider(siteProvider, playerParseProvider, shikiSiteProvider);
            var shikiItem = new ItemInfo(siteProvider.Site, shikimoriId.ToString());
            var nekoItems = await searchProvider.FindSimilarAsync(shikiItem, CancellationToken.None).ConfigureAwait(false);

            provider.InitForItems(nekoItems);

            var files = await ProviderTestHelpers
                .PreloadAndGetFirstOfType<File>(provider, new Folder(provider.Site, "", FolderType.ProviderRoot, PositionBehavior.None))
                .ConfigureAwait(false);

            await ProviderTestHelpers
                .CheckFilesAsync(
                    files,
                    CheckFileFlags.AtLeastOne | CheckFileFlags.IgnoreVideos | CheckFileFlags.Playlist)
                .ConfigureAwait(false);
        }
    }
}

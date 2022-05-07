namespace FSClient.Providers.Test.Nekomori
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class NekomoriFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return Task.WhenAll(
                new ShikiSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync(),
                new NekomoriSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync());
        }

        [Timeout(30000)]
        [TestCase(31764, "Nejimaki Seirei Senki: Tenkyou no Alderamin")]
        [TestCase(42361, "Ijiranaide, Nagatoro-san")]
        public async Task ShikiNekomoriFileProvider_LoadFolder_Files_From_Shikimori(int shikimoriId, string titleOrigin)
        {
            var siteProvider = new NekomoriSiteProvider(Test.ProviderConfigService);
            var shikiSearchProvider = new ShikiSearchProvider(new ShikiSiteProvider(Test.ProviderConfigService), null);
            var nekoSearchProvider = new NekomoriSearchProvider(siteProvider, shikiSearchProvider);
            var provider = new NekomoriFileProvider(siteProvider, new PlayerParseManagerStub());
            var shikiItem = new ItemInfo(shikiSearchProvider.Site, shikimoriId.ToString())
            {
                Section = Section.CreateDefault(SectionModifiers.Anime),
                Details =
                {
                    TitleOrigin = titleOrigin
                }
            };
            var nekoItems = await nekoSearchProvider.FindSimilarAsync(shikiItem, CancellationToken.None).ConfigureAwait(false);

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

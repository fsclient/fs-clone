namespace FSClient.Providers.Test.FindAnime
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class FindAnimeFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new FindAnimeSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("van_pis__tv_")]
        [TestCase("belaia_zmeia__proishojdenie")]
        public async Task FindAnimeFileProvider_LoadFolder_Root(string id)
        {
            var siteProvider = new FindAnimeSiteProvider(Test.ProviderConfigService);
            var provider = new FindAnimeFileProvider(siteProvider, new PlayerParseManagerStub());
            var item = new ItemInfo(provider.Site, id)
            {
                Link = new Uri("https://fake.link")
            };

            provider.InitForItems(new[] { item });
            var folder = new Folder(provider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);
            var result = await provider.GetFolderChildrenAsync(folder, CancellationToken.None).ConfigureAwait(false);
            Assert.That(result.Any(), Is.True, "Folder items source is empty");
        }

        [Timeout(20000)]
        [TestCase("/van_pis__tv_/series889")]
        [TestCase("/vedmak__koshmar_volka_/series0")]
        public async Task FindAnimeFileProvider_LoadFolder_Files(string playerPage)
        {
            var siteProvider = new FindAnimeSiteProvider(Test.ProviderConfigService);
            var provider = new FindAnimeFileProvider(siteProvider, new PlayerParseManagerStub());

            var folder = new FindAnimeFileProvider.FindAnimeFolder(siteProvider.Site, "", new Uri(playerPage, UriKind.Relative), FolderType.Episode, PositionBehavior.None);

            var result = await provider.GetFolderChildrenAsync(folder, CancellationToken.None).ConfigureAwait(false);

            var files = result.OfType<File>().ToArray();
            await ProviderTestHelpers
                .CheckFilesAsync(
                    files,
                    CheckFileFlags.AtLeastOne | CheckFileFlags.IgnoreVideos | CheckFileFlags.Playlist)
                .ConfigureAwait(false);
        }

        [Timeout(20000)]
        [TestCase("van_pis__tv_")]
        public async Task FindAnimeFileProvider_GetTrailers(string id)
        {
            var siteProvider = new FindAnimeSiteProvider(Test.ProviderConfigService);
            var provider = new FindAnimeFileProvider(siteProvider, new PlayerParseManagerStub());
            var item = new ItemInfo(provider.Site, id)
            {
                Link = new Uri("https://fake.link")
            };

            var nodes = await provider.GetTrailersRootAsync(item, CancellationToken.None).ConfigureAwait(false);
            var files = ProviderTestHelpers.GetDeepNodes<File>(nodes!);

            await ProviderTestHelpers.CheckFilesAsync(files, CheckFileFlags.Default | CheckFileFlags.Trailers | CheckFileFlags.IgnoreVideos).ConfigureAwait(false);
        }
    }
}

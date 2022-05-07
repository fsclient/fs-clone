namespace FSClient.Providers.Test.Anitube
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
    public class AnitubeFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new AnitubeSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase(3613, "3613-uzaki-chan-wa-asobitai.html")]
        public async Task AnitubeFileProvider_LoadFolder_Root(int id, string link)
        {
            var siteProvider = new AnitubeSiteProvider(Test.ProviderConfigService);
            var provider = new AnitubeFileProvider(siteProvider, new PlayerParseManagerStub());
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Link = new Uri(link, UriKind.Relative)
            };

            provider.InitForItems(new[] { item });
            var folder = new Folder(provider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);

            var files = await ProviderTestHelpers.PreloadAndGetDeepNodes<File>(provider, folder).ConfigureAwait(false);
            await ProviderTestHelpers.CheckFilesAsync(files, CheckFileFlags.Default | CheckFileFlags.IgnoreVideos).ConfigureAwait(false);
        }

        [Timeout(20000)]
        [TestCase(3613, "3613-uzaki-chan-wa-asobitai.html")]
        public async Task AnitubeFileProvider_GetTrailers(int id, string link)
        {
            var siteProvider = new AnitubeSiteProvider(Test.ProviderConfigService);
            var provider = new AnitubeFileProvider(siteProvider, new PlayerParseManagerStub());
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

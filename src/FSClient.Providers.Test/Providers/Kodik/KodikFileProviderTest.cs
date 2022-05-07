namespace FSClient.Providers.Test.Kodik
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class KodikFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new KodikSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(60000)]
        [TestCase(1249511, "/serial/2046/d73994954a08782fddc68295cc8d072a/720p", true)]
        [TestCase(1249511, "/serial/5895/cceb79c981801b4be03bead433feac50/720p", true)]
        public async Task KodikFileProvider_LoadFolder_Full(int kpId, string link, bool isSerial)
        {
            var siteProvider = new KodikSiteProvider(Test.ProviderConfigService);
            var item = new ItemInfo(Site.Any, "kodik" + kpId)
            {
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film),
                Link = new Uri(link, UriKind.Relative),
                Details =
                {
                    LinkedIds =
                    {
                        [Sites.Kinopoisk] = kpId.ToString()
                    }
                }
            };
            var provider = new KodikFileProvider(siteProvider, new KodikPlayerParseProvider(siteProvider));
            var searchProvider = new KodikSearchProvider(siteProvider);
            var items = await searchProvider.FindSimilarAsync(item, CancellationToken.None).ConfigureAwait(false);

            provider.InitForItems(items);
            var folder = new Folder(siteProvider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);

            var files = await ProviderTestHelpers.PreloadAndGetDeepNodes<File>(provider, folder).ConfigureAwait(false);

            await ProviderTestHelpers
                .CheckFilesAsync(files, isSerial
                    ? CheckFileFlags.UniqueIDs | CheckFileFlags.AtLeastOne | CheckFileFlags.Serial | CheckFileFlags.IgnoreVideos
                    : CheckFileFlags.UniqueIDs | CheckFileFlags.AtLeastOne | CheckFileFlags.IgnoreVideos)
                .ConfigureAwait(false);
        }
    }
}

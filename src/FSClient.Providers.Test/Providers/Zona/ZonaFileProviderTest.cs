namespace FSClient.Providers.Test.Zona
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class ZonaFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ZonaSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(60000)]
        [TestCase(2592416, "/tvseries/vikingi-2013/season-2", true)]     // Vikings
        [TestCase(2377637, "/movies/doktor-strendzh-2016", false)]     // Doctor Strange
        public async Task ZonaFileProvider_LoadFolder_Full(int id, string link, bool isSerial)
        {
            var siteProvider = new ZonaSiteProvider(Test.ProviderConfigService);
            var provider = new ZonaFileProvider(siteProvider);
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film),
                Title = link, // don't care
                Link = new Uri(link, UriKind.Relative)
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
    }
}

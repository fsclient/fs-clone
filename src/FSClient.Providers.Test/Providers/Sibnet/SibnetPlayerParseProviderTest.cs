namespace FSClient.Providers.Test.Sibnet
{
    using System;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.PlayerParseProvider)]
    public class SibnetPlayerParseProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new SibnetSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("http://video.sibnet.ru/shell.php?videoid=2824924")]
        public void SibnetPlayerParseProvider_CanOpenFromLinkOrHostingName_Success(string url)
        {
            var player = new SibnetPlayerParseProvider(new SibnetSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.True);
        }

        [Timeout(20000)]
        [TestCase("https://www.youtube.com/watch?v=LiZRgX2Y2hk")]
        public void SibnetPlayerParseProvider_CanOpenFromLinkOrHostingName_Fail(string url)
        {
            var player = new SibnetPlayerParseProvider(new SibnetSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.False);
        }

        [Timeout(20000)]
        [TestCase("http://video.sibnet.ru/shell.php?videoid=2824924")]
        [TestCase("http://video.sibnet.ru/video2552482-One_Piece_736_russkaya_ozvuchka_Skim___Van_Pis___736_seriya_Risens_Team/")]
        public async Task SibnetPlayerParseProvider_ParseFromUriAsync(string url)
        {
            var player = new SibnetPlayerParseProvider(new SibnetSiteProvider(Test.ProviderConfigService));
            var file = await player.ParseFromUriAsync(new Uri(url), default).ConfigureAwait(false);

            await ProviderTestHelpers.CheckFileAsync(file);
        }
    }
}

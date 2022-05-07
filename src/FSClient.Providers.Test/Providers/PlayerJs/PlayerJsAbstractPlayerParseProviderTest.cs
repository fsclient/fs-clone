namespace FSClient.Providers.Test.PlayerJsAbstract
{
    using System;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.PlayerParseProvider)]
    public class PlayerJsAbstractPlayerParseProviderTest
    {
        [Timeout(20000)]
        [TestCase("https://secvideo1.online/embed/202358")]
        [TestCase("https://ashdi.vip/vod/27752")]
        [TestCase("https://tortuga.wtf/vod/42655")]
        public void PlayerJsAbstractPlayerParseProvider_CanOpenFromLinkOrHostingName_Success(string url)
        {
            var player = new PlayerJsAbstractPlayerParseProvider(
                new PlayerJsAbstractSiteProvider(Test.ProviderConfigService),
                new PlayerJsParserService(Test.SettingService, Test.Logger));

            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.True);
        }

        [Timeout(20000)]
        [TestCase("https://www.youtube.com/watch?v=LiZRgX2Y2hk")]
        public void PlayerJsAbstractPlayerParseProvider_CanOpenFromLinkOrHostingName_Fail(string url)
        {
            var player = new PlayerJsAbstractPlayerParseProvider(
                new PlayerJsAbstractSiteProvider(Test.ProviderConfigService),
                new PlayerJsParserService(Test.SettingService, Test.Logger));

            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.False);
        }

        [Timeout(20000)]
        //[TestCase("https://protonvideo.to/iframe/c635bcbdf4fc17bab30d348705b78c00/")]
        [TestCase("https://secvideo1.online/embed/202358")]
        [TestCase("https://ashdi.vip/vod/27752")]
        [TestCase("https://tortuga.wtf/vod/42655")]
        public async Task PlayerJsAbstractPlayerParseProvider_ParseFromUriAsync(string url)
        {
            var player = new PlayerJsAbstractPlayerParseProvider(
                new PlayerJsAbstractSiteProvider(Test.ProviderConfigService),
                new PlayerJsParserService(Test.SettingService, Test.Logger));

            var file = await player.ParseFromUriAsync(new Uri(url), default).ConfigureAwait(false);

            await ProviderTestHelpers.CheckFileAsync(file);
        }
    }
}

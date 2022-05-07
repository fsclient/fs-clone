namespace FSClient.Providers.Test.AnimeJoy
{
    using System;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.PlayerParseProvider)]
    public class AnimeJoyPlayerParseProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new AnimeJoySiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("https://animejoy.ru/player/playerjs.html?file=[1080p]https://animej.site/Mashiro_no_Oto/01_1080.mp4,[360p]https://animej.site/Mashiro_no_Oto/01_360p.mp4")]
        public void AnimeJoyPlayerParseProvider_CanOpenFromLinkOrHostingName_Success(string url)
        {
            var player = new AnimeJoyPlayerParseProvider(new AnimeJoySiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.True);
        }

        [Timeout(20000)]
        [TestCase("https://www.youtube.com/watch?v=LiZRgX2Y2hk")]
        [TestCase("https://animejoy.ru/player/playerjs.html?notafile=[1080p]https://animej.site/Mashiro_no_Oto/01_1080.mp4,[360p]https://animej.site/Mashiro_no_Oto/01_360p.mp4")]
        public void AnimeJoyPlayerParseProvider_CanOpenFromLinkOrHostingName_Fail(string url)
        {
            var player = new AnimeJoyPlayerParseProvider(new AnimeJoySiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.False);
        }

        [Timeout(20000)]
        [TestCase("https://animejoy.ru/player/playerjs.html?file=[1080p]https://animej.site/Mashiro_no_Oto/01_1080.mp4,[360p]https://animej.site/Mashiro_no_Oto/01_360p.mp4")]
        public async Task AnimeJoyPlayerParseProvider_ParseFromUriAsync(string url)
        {
            var player = new AnimeJoyPlayerParseProvider(new AnimeJoySiteProvider(Test.ProviderConfigService));
            var file = await player.ParseFromUriAsync(new Uri(url), default).ConfigureAwait(false);

            await ProviderTestHelpers.CheckFileAsync(file);
        }
    }
}

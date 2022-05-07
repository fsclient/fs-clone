namespace FSClient.Providers.Test.Youtube
{
    using System;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.PlayerParseProvider)]
    public class YoutubePlayerParseProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new YoutubeSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("https://www.youtube.com/watch?v=LiZRgX2Y2hk")]
        public void YoutubePlayerParseProvider_CanOpenFromLinkOrHostingName_Success(string url)
        {
            var player = new YoutubePlayerParseProvider(new YoutubeSiteProvider(Test.ProviderConfigService), Test.Logger);
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.True);
        }

        [Timeout(20000)]
        [TestCase("https://www.rutube.com/watch?v=LiZRgX2Y2hk")]
        public void YoutubePlayerParseProvider_CanOpenFromLinkOrHostingName_Fail(string url)
        {
            var player = new YoutubePlayerParseProvider(new YoutubeSiteProvider(Test.ProviderConfigService), Test.Logger);
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.False);
        }

        [Timeout(20000)]
        [TestCase("https://www.youtube.com/watch?v=LiZRgX2Y2hk")]
        public async Task YoutubePlayerParseProvider_ParseFromUriAsync(string url)
        {
            var player = new YoutubePlayerParseProvider(new YoutubeSiteProvider(Test.ProviderConfigService), Test.Logger);
            var file = await player.ParseFromUriAsync(new Uri(url), default).ConfigureAwait(false);

            await ProviderTestHelpers.CheckFileAsync(file);
        }
    }
}

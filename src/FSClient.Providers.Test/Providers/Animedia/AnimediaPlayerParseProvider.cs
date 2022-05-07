namespace FSClient.Providers.Test.Animedia
{
    using System;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.PlayerParseProvider)]
    public class AnimediaPlayerParseProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new AnimediaSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("https://online.animedia.tv/embed/15894/1/14")]
        public void AnimediaPlayerParseProvider_CanOpenFromLinkOrHostingName_Success(string url)
        {
            var player = new AnimediaPlayerParseProvider(new AnimediaSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.True);
        }

        [Timeout(20000)]
        [TestCase("https://www.youtube.com/watch?v=LiZRgX2Y2hk")]
        public void AnimediaPlayerParseProvider_CanOpenFromLinkOrHostingName_Fail(string url)
        {
            var player = new AnimediaPlayerParseProvider(new AnimediaSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.False);
        }

        [Timeout(20000)]
        [TestCase("https://online.animedia.tv/embed/15894/1/14")]
        public async Task AnimediaPlayerParseProvider_ParseFromUriAsync(string url)
        {
            var player = new AnimediaPlayerParseProvider(new AnimediaSiteProvider(Test.ProviderConfigService));
            var file = await player.ParseFromUriAsync(new Uri(url), default).ConfigureAwait(false);

            await ProviderTestHelpers.CheckFileAsync(file);
        }
    }
}

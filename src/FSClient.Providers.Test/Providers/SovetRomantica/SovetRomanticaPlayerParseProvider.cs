namespace FSClient.Providers.Test.SovetRomantica
{
    using System;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.PlayerParseProvider)]
    [Ignore("DDoS-GUARD")]
    public class SovetRomanticaPlayerParseProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new SovetRomanticaSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("http://sovetromantica.com/embed/episode_244_1-dubbed")]
        [TestCase("https://sovetromantica.com/embed/episode_841_9-subtitles")]
        public void SovetRomanticaPlayerParseProvider_CanOpenFromLinkOrHostingName_Success(string url)
        {
            var player = new SovetRomanticaPlayerParseProvider(new SovetRomanticaSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.True);
        }

        [Timeout(20000)]
        [TestCase("https://www.youtube.com/watch?v=LiZRgX2Y2hk")]
        public void SovetRomanticaPlayerParseProvider_CanOpenFromLinkOrHostingName_Fail(string url)
        {
            var player = new SovetRomanticaPlayerParseProvider(new SovetRomanticaSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.False);
        }

        [Timeout(20000)]
        [TestCase("http://sovetromantica.com/embed/episode_244_1-dubbed")]
        public async Task SovetRomanticaPlayerParseProvider_ParseFromUriAsync(string url)
        {
            var player = new SovetRomanticaPlayerParseProvider(new SovetRomanticaSiteProvider(Test.ProviderConfigService));
            var file = await player.ParseFromUriAsync(new Uri(url), default).ConfigureAwait(false);

            await ProviderTestHelpers.CheckFileAsync(file);
        }
    }
}

namespace FSClient.Providers.Test.Kodik
{
    using System;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.PlayerParseProvider)]
    public class KodikPlayerParseProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new KodikSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("http://kodik.cc/serial/2046/d73994954a08782fddc68295cc8d072a/720p")]
        [TestCase("http://kodik.cc/video/1704/f89980fbd13e8154d3ea137848fd8350/720p")]
        [TestCase("http://kodik.info/go/seria/454701/3fbfea0db147576d7328b3f939954f5a/720p")]
        public void KodikPlayerParseProvider_CanOpenFromLinkOrHostingName_Success(string url)
        {
            var player = new KodikPlayerParseProvider(new KodikSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.True);
        }

        [Timeout(20000)]
        [TestCase("https://www.youtube.com/watch?v=LiZRgX2Y2hk")]
        public void KodikPlayerParseProvider_CanOpenFromLinkOrHostingName_Fail(string url)
        {
            var player = new KodikPlayerParseProvider(new KodikSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.False);
        }

        [Timeout(20000)]
        [TestCase("/seria/687506/ac4452ecb58e66d6d46444c9611cc376/720p")]
        public async Task KodikPlayerParseProvider_ParseFromUriAsync(string url)
        {
            var player = new KodikPlayerParseProvider(new KodikSiteProvider(Test.ProviderConfigService));
            var file = await player.ParseFromUriAsync(new Uri(url, UriKind.Relative), default).ConfigureAwait(false);

            await ProviderTestHelpers.CheckFileAsync(file);
        }
    }
}

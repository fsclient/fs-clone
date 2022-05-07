namespace FSClient.Providers.Test.Myvi
{
    using System;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.PlayerParseProvider)]
    public class MyviPlayerParseProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new MyviSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("http://myvi.ru/player/embed/html/o5qmFAtCDopRv1ks_SUglRribC4301uIqjJ7I8iLZYxBQO2knzZ86UkI3dODlM5PG0")]
        public void MyviPlayerParseProvider_CanOpenFromLinkOrHostingName_Success(string url)
        {
            var player = new MyviPlayerParseProvider(new MyviSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.True);
        }

        [Timeout(20000)]
        [TestCase("https://www.youtube.com/watch?v=LiZRgX2Y2hk")]
        public void MyviPlayerParseProvider_CanOpenFromLinkOrHostingName_Fail(string url)
        {
            var player = new MyviPlayerParseProvider(new MyviSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.False);
        }

        [Timeout(20000)]
        [TestCase("http://myvi.ru/player/embed/html/o5qmFAtCDopRv1ks_SUglRribC4301uIqjJ7I8iLZYxBQO2knzZ86UkI3dODlM5PG0")]
        public async Task MyviPlayerParseProvider_ParseFromUriAsync(string url)
        {
            var player = new MyviPlayerParseProvider(new MyviSiteProvider(Test.ProviderConfigService));
            var file = await player.ParseFromUriAsync(new Uri(url), default).ConfigureAwait(false);

            await ProviderTestHelpers.CheckFileAsync(file);
        }
    }
}

namespace FSClient.Providers.Test.SmotretAnime
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Moq;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.PlayerParseProvider)]
    public class SmotretAnimePlayerParseProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new SmotretAnimeSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(20000)]
        [TestCase("https://smotretanime.ru/catalog/kissxsis-4306/ova-0-seriya-11983/russkie-subtitry-1095217")]
        public void SmotretAnimePlayerParseProvider_CanOpenFromLinkOrHostingName_Success(string url)
        {
            var player = new SmotretAnimePlayerParseProvider(new SmotretAnimeSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.True);
        }

        [Timeout(20000)]
        [TestCase("https://www.youtube.com/watch?v=LiZRgX2Y2hk")]
        public void SmotretAnimePlayerParseProvider_CanOpenFromLinkOrHostingName_Fail(string url)
        {
            var player = new SmotretAnimePlayerParseProvider(new SmotretAnimeSiteProvider(Test.ProviderConfigService));
            var valid = player.CanOpenFromLinkOrHostingName(new Uri(url));

            Assert.That(valid, Is.False);
        }

        [Timeout(20000)]
        [Ignore("Missed premium for test user")]
        [TestCase("https://smotretanime.ru/catalog/kissxsis-4306/ova-0-seriya-11983/russkie-subtitry-1095217", true)]
        public async Task SmotretAnimePlayerParseProvider_ParseFromUriAsync(string url, bool subs)
        {
            Assume.That(Secrets.Test.SAPassword, Is.Not.Null & Is.Not.Empty, "Password is missed");
            Assume.That(Secrets.Test.SAUserName, Is.Not.Null & Is.Not.Empty, "UserName is missed");

            var contentDialogMock = new Mock<IContentDialog<LoginDialogOutput>>()
                .SetupAllProperties();

            contentDialogMock.Setup(m => m.ShowAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                () => new LoginDialogOutput
                {
                    Login = Secrets.Test.SAUserName,
                    Password = Secrets.Test.SAPassword,
                    Status = AuthStatus.Success
                });

            var saSiteProvider = new SmotretAnimeSiteProvider(Test.ProviderConfigService);
            var saAuthProvider = new SmotretAnimeAuthProvider(saSiteProvider, contentDialogMock.Object);
            var player = new SmotretAnimePlayerParseProvider(saSiteProvider);
            var authModel = saAuthProvider.AuthModels.First(m => !m.IsOAuth);

            var userManager = new UserManager(new[] { saAuthProvider }, new[] { saSiteProvider }, new CookieManagerStub(), null!, Test.Logger);
            var (user, status) = await userManager.AuthorizeAsync(saSiteProvider.Site, authModel, CancellationToken.None).ConfigureAwait(false);

            Assert.That(user, Is.Not.Null, "User is null");

            var isAllowed = await userManager.CheckRequirementsAsync(saSiteProvider.Site, player.ReadRequirements, default).ConfigureAwait(false);
            Assert.That(isAllowed, Is.True, "Files are not allowed");

            Assert.That(user, Is.Not.Null, "User is null");

            var file = await player.ParseFromUriAsync(new Uri(url), default).ConfigureAwait(false);

            await ProviderTestHelpers.CheckFileAsync(file, checkSubs: subs);
        }
    }
}

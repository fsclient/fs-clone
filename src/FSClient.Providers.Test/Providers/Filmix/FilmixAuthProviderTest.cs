namespace FSClient.Providers.Test.Filmix
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Moq;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.AuthProvider)]
    public class FilmixAuthProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new FilmixSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Test]
        [Timeout(6 * 60 * 1000)]
        public async Task FilmixAuthProvider_AuthorizeAsync_NoOAuth()
        {
            Assume.That(Secrets.Test.FilmixPassword, Is.Not.Null & Is.Not.Empty, "Password is missed");
            Assume.That(Secrets.Test.FilmixUserName, Is.Not.Null & Is.Not.Empty, "UserName is missed");

            var contentDialogMock = new Mock<IContentDialog<LoginDialogOutput>>()
                .SetupAllProperties();

            contentDialogMock.Setup(m => m.ShowAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                () => new LoginDialogOutput
                {
                    Login = Secrets.Test.FilmixUserName,
                    Password = Secrets.Test.FilmixPassword,
                    Status = AuthStatus.Success
                });

            var provider = new FilmixAuthProvider(new FilmixSiteProvider(Test.ProviderConfigService), null!, contentDialogMock.Object);
            var authModel = provider.AuthModels.First(m => !m.IsOAuth);
            var result = await provider.AuthorizeAsync(authModel, CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.User, Is.Not.Null, "User is null");
            Assert.That(string.IsNullOrWhiteSpace(result.User!.Nickname), Is.False, "User nickname is null or whitespace");
        }
    }
}

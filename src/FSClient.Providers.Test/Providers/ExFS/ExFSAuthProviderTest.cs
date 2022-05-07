namespace FSClient.Providers.Test.ExFS
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
    public class ExFSAuthProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ExFSSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Test]
        [Timeout(60000)]
        public async Task ExFSAuthProvider_AuthorizeAsync_NoOAuth()
        {
            Assume.That(Secrets.Test.ExFSPassword, Is.Not.Null & Is.Not.Empty, "Password is missed");
            Assume.That(Secrets.Test.ExFSUserName, Is.Not.Null & Is.Not.Empty, "UserName is missed");

            var contentDialogMock = new Mock<IContentDialog<LoginDialogOutput>>()
                .SetupAllProperties();

            contentDialogMock.Setup(m => m.ShowAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                () => new LoginDialogOutput
                {
                    Login = Secrets.Test.ExFSUserName,
                    Password = Secrets.Test.ExFSPassword,
                    Status = AuthStatus.Success
                });

            var provider = new ExFSAuthProvider(new ExFSSiteProvider(Test.ProviderConfigService), null!, contentDialogMock.Object);
            var authModel = provider.AuthModels.First(m => !m.IsOAuth);
            var result = await provider.AuthorizeAsync(authModel, CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.User, Is.Not.Null, "User is null");
            Assert.That(string.IsNullOrWhiteSpace(result.User!.Nickname), Is.False, "User nickname is null or whitespace");
        }
    }
}

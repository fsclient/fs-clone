namespace FSClient.Providers.Test.SeasonVar
{
    using System;
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
    public class SeasonVarAuthProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new SeasonVarSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Test]
        [Timeout(60000)]
        public async Task SeasonVarAuthProvider_AuthorizeAsync_NoOAuth()
        {
            Assume.That(Secrets.Test.SeasonVarPassword, Is.Not.Null & Is.Not.Empty, "Password is missed");
            Assume.That(Secrets.Test.SeasonVarUserName, Is.Not.Null & Is.Not.Empty, "UserName is missed");

            var contentDialogMock = new Mock<IContentDialog<LoginDialogOutput>>()
                .SetupAllProperties();

            contentDialogMock.Setup(m => m.ShowAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                () => new LoginDialogOutput
                {
                    Login = Secrets.Test.SeasonVarUserName,
                    Password = Secrets.Test.SeasonVarPassword,
                    Status = AuthStatus.Success
                });

            var siteProvider = new SeasonVarSiteProvider(Test.ProviderConfigService);
            var domain = await siteProvider.GetMirrorAsync(default).ConfigureAwait(false);
            if (domain.Host.Contains("herokuapp", StringComparison.Ordinal))
            {
                Assert.Inconclusive("Current mirror doesn't support Auth");
            }
            var provider = new SeasonVarAuthProvider(siteProvider, contentDialogMock.Object);
            var authModel = provider.AuthModels.First(m => !m.IsOAuth);
            var result = await provider.AuthorizeAsync(authModel, CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.User, Is.Not.Null, "User is null");
            Assert.That(string.IsNullOrWhiteSpace(result.User!.Nickname), Is.False, "User nickname is null or whitespace");
        }
    }
}

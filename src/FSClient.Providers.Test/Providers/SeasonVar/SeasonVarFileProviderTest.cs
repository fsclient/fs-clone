namespace FSClient.Providers.Test.SeasonVar
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Moq;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.FileProvider)]
    public class SeasonVarFileProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new SeasonVarSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Timeout(60000)]
        [TestCase(18127, "serial-18127-Arkadiya_moej_yunosti_SSX_Beskonechnyj_put_.html", true)]
        public async Task SeasonVarFileProvider_LoadFolder_Full(int id, string link, bool subs)
        {
            var siteProvider = new SeasonVarSiteProvider(Test.ProviderConfigService);
            var provider = new SeasonVarFileProvider(siteProvider, new PlayerJsParserService(Test.SettingService, Test.Logger));

            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Link = new Uri(link, UriKind.Relative)
            };
            var items = await new SeasonVarSearchProvider(siteProvider).FindSimilarAsync(item, CancellationToken.None).ConfigureAwait(false);

            provider.InitForItems(items);
            var folder = new Folder(siteProvider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);

            var files = await ProviderTestHelpers.PreloadAndGetDeepNodes<File>(provider, folder).ConfigureAwait(false);

            await ProviderTestHelpers
                .CheckFilesAsync(files, CheckFileFlags.UniqueIDs
                    | CheckFileFlags.AtLeastOne
                    | CheckFileFlags.Serial
                    | (subs ? CheckFileFlags.WithSubs : CheckFileFlags.None), -1)
                .ConfigureAwait(false);
        }

        [Timeout(60000)]
        [TestCase(14419, "serial-14419-WWWRabota.html", false)]
        public async Task SeasonVarFileProvider_LoadFolder_Full_With_Auth(int id, string link, bool subs)
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

            var authProvider = new SeasonVarAuthProvider(siteProvider, contentDialogMock.Object);
            var provider = new SeasonVarFileProvider(siteProvider, new PlayerJsParserService(Test.SettingService, Test.Logger));
            var authModel = authProvider.AuthModels.First(m => !m.IsOAuth);
            var item = new ItemInfo(provider.Site, id.ToString())
            {
                Link = new Uri(link, UriKind.Relative)
            };

            var userManager = new UserManager(new[] { authProvider }, new[] { siteProvider }, new CookieManagerStub(), null!, Test.Logger);
            var (user, status) = await userManager.AuthorizeAsync(provider.Site, authModel, CancellationToken.None).ConfigureAwait(false);

            Assert.That(user, Is.Not.Null, "User is null");

            var isAllowed = await userManager.CheckRequirementsAsync(provider.Site, provider.ReadRequirements, default).ConfigureAwait(false);
            Assert.That(isAllowed, Is.True, "Files are not allowed");

            var items = await new SeasonVarSearchProvider(siteProvider).FindSimilarAsync(item, CancellationToken.None).ConfigureAwait(false);
            provider.InitForItems(items);
            var folder = new Folder(siteProvider.Site, "", FolderType.ProviderRoot, PositionBehavior.None);

            var files = await ProviderTestHelpers.PreloadAndGetDeepNodes<File>(provider, folder).ConfigureAwait(false);

            await ProviderTestHelpers
                .CheckFilesAsync(files, CheckFileFlags.UniqueIDs
                    | CheckFileFlags.AtLeastOne
                    | CheckFileFlags.RequireHD
                    | CheckFileFlags.Serial
                    | (subs ? CheckFileFlags.WithSubs : CheckFileFlags.None), -1)
                .ConfigureAwait(false);
        }
    }
}

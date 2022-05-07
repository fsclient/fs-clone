namespace FSClient.Providers.Test.Filmix
{
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

    [TestFixture(Category = TestCategories.FavoriteProvider)]
    public class FilmixFavoritesProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new FilmixSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [Test]
        [Timeout(6 * 60 * 1000)]
        public async Task FilmixFavoritesProvider_GetItemsAsync()
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

            var siteProvider = new FilmixSiteProvider(Test.ProviderConfigService);
            var provider = new FilmixAuthProvider(siteProvider, null!, contentDialogMock.Object);
            var usersManager = new UserManager(new[] { provider }, new[] { siteProvider }, new CookieManagerStub(), null!, Test.Logger);
            var (user, status) = await usersManager.AuthorizeAsync(siteProvider.Site, usersManager.GetAuthModels(siteProvider.Site).First(m => !m.IsOAuth), CancellationToken.None).ConfigureAwait(false);
            Assert.That(user?.Nickname, Is.Not.Null, "Authorize failled");

            var favProvider = new FilmixFavoriteProvider(siteProvider);

            var isAllowed = await usersManager.CheckRequirementsAsync(favProvider.Site, ProviderRequirements.AccountForAny, default).ConfigureAwait(false);

            Assert.That(isAllowed, Is.True, "Favs are not allowed.");

            var pages = await Task.WhenAll(favProvider
                .AvailableListKinds
                .Select(t => favProvider
                    .GetItemsAsync(t, CancellationToken.None)))
                .ConfigureAwait(false);
            Assert.That(pages.Length > 0, Is.True, "No pages found");
            CollectionAssert.AllItemsAreNotNull(pages, "Some page is null");
            CollectionAssert.AllItemsAreUnique(pages, "Pages is not unique");

            foreach (var page in pages)
            {
                var collection = page.ToArray();
                Assert.That(collection.Length > 0, Is.True, "Some pages has not items");
                CollectionAssert.AllItemsAreNotNull(collection, "Some page has null items");
                CollectionAssert.AllItemsAreUnique(collection, "Some page items is not unique");
                foreach (var item in collection)
                {
                    Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                    Assert.That(item.Link, Is.Not.Null, "Item link is null");
                    Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                    Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                    Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                    Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is undefined");
                }
            }
        }
    }
}

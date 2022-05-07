namespace FSClient.Providers.Test.Filmix
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

    [TestFixture(Category = TestCategories.SearchProvider)]
    [Ignore("Dead mirror")]
    public class FilmixProSearchProviderTest
    {
        private readonly Uri proMirror = new Uri("https://filmix.tech");

        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new FilmixSiteProvider(new ProviderServiceStub(Sites.Filmix, proMirror)).CheckIsAvailableAsync();
        }

        [TestCase("Кинг")]
        [Timeout(6 * 60 * 1000)]
        public async Task FilmixSearchProvider_GetFullResultAsync_ProMirror(string query)
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

            var siteProvider = new FilmixSiteProvider(new ProviderServiceStub(Sites.Filmix, proMirror));
            var authProvider = new FilmixAuthProvider(siteProvider, null!, contentDialogMock.Object);
            var userManager = new UserManager(new[] { authProvider }, new[] { siteProvider }, new CookieManagerStub(), null!, Test.Logger);
            var (user, status) = await userManager.AuthorizeAsync(siteProvider.Site, userManager.GetAuthModels(siteProvider.Site).First(m => !m.IsOAuth), CancellationToken.None).ConfigureAwait(false);

            var provider = new FilmixSearchProvider(siteProvider);

            Assert.That(user, Is.Not.Null, "User is null");

            var isAllowed = await userManager.CheckRequirementsAsync(provider.Site, provider.ReadRequirements, default).ConfigureAwait(false);
            Assert.That(isAllowed, Is.True, "Files are not allowed");

            var section = provider.Sections.First(s => s != Section.Any);

            var page = await provider.GetSearchPageParamsAsync(section, default);
            var filter = new SearchPageFilter(page!, query);

            var resultItems = await provider.GetFullResult(filter).Take(30).ToListAsync();

            Assert.That(resultItems, Is.Not.Empty, "Zero items count on page");

            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(item.Section, Is.EqualTo(section), "Item section is not match");
            }
        }
    }
}

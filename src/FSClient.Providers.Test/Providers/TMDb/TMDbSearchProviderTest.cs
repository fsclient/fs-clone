namespace FSClient.Providers.Test.TMDb
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class TMDbSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub()).CheckIsAvailableAsync();
        }

        [TestCase("Игра", SectionModifiers.Serial)]
        [TestCase("Game", SectionModifiers.Film)]
        [Timeout(30000)]
        public async Task TMDbSearchProvider_GetFullResultAsync(string query, SectionModifiers sectionModifier)
        {
            var siteProvider = new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub());
            var cahceService = new CacheServiceStub((_, __) => Task.FromResult((object)Array.Empty<TitledTag>()));
            var provider = new TMDbSearchProvider(siteProvider, new TMDbItemInfoProvider(siteProvider, cahceService));

            var section = provider.Sections.First(s => s != Section.Any && s.Modifier.HasFlag(sectionModifier));

            var page = await provider.GetSearchPageParamsAsync(section, default);
            var filter = new SearchPageFilter(page!, query);

            var resultItems = await provider.GetFullResult(filter).Take(10).ToListAsync();

            Assert.That(resultItems, Is.Not.Empty, "Zero items count on page");

            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            Assert.That(resultItems.All(i => i.Site == provider.Site), Is.True, "Item site is invalid");
            Assert.That(resultItems.All(i => i.Section == section), Is.True, "Item section is not match");
            Assert.That(resultItems.All(i => !i.Poster.Any() || i.Poster.All(p => p.Value.IsAbsoluteUri)), Is.True,
                "Item poster image is not absolute uri");
            Assert.That(resultItems.Any(i => i.Poster.Any()), Is.True, "No items with poster");
            Assert.That(resultItems.Any(i => i.Link != null), Is.True, "Item link is null");
            Assert.That(resultItems.Any(i => i.Details.Year != null), Is.True, "Item year is null");
            Assert.That(resultItems.Any(i => !string.IsNullOrWhiteSpace(i.Title)), Is.True, "Item title is empty");
            Assert.That(resultItems.Any(i => !string.IsNullOrWhiteSpace(i.Details.TitleOrigin)), Is.True, "Item origin title is empty");
        }
    }
}

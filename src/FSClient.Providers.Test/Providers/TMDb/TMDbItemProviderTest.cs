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

    [TestFixture(Category = TestCategories.ItemProvider)]
    public class TMDbItemProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub()).CheckIsAvailableAsync();
        }

        [TestCase(SectionModifiers.Serial)]
        [TestCase(SectionModifiers.Film)]
        [Timeout(10000)]
        public async Task TMDbItemProvider_GetFullResultAsync(SectionModifiers sectionModifier)
        {
            var siteProvider = new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub());
            var cahceService = new CacheServiceStub((_, __) => Task.FromResult((object)Array.Empty<TitledTag>()));
            var provider = new TMDbItemProvider(siteProvider, new TMDbItemInfoProvider(siteProvider, cahceService), cahceService);

            var section = provider.Sections.First(s => s != Section.Any && s.Modifier.HasFlag(sectionModifier));

            var page = await provider.GetSectionPageParamsAsync(section, default);
            var filter = new SectionPageFilter(page!);

            var resultItems = await provider.GetFullResult(filter!).Take(30).ToListAsync();

            Assert.That(resultItems, Is.Not.Empty, "Zero items count on page");
            Assert.That(resultItems.Count, Is.EqualTo(30), "Missing items");

            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            Assert.That(resultItems.Any(i => i.Poster.Any()), Is.True, "No items with poster");
            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
                Assert.That(!item.Poster.Any() || item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(string.IsNullOrWhiteSpace(item.Details.TitleOrigin), Is.False, "Item origin title is empty");
                Assert.That(item.Section, Is.EqualTo(section), "Item section is not match");
            }
        }
    }
}

namespace FSClient.Providers.Test.Rezka
{
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Providers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemProvider)]
    public class RezkaItemProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new RezkaSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase(1)]
        [TestCase(2)]
        [Timeout(120000)]
        public async Task RezkaItemProvider_GetItemsAsync(int sectionNumber)
        {
            var siteProvider = new RezkaSiteProvider(Test.ProviderConfigService);
            var provider = new RezkaItemProvider(siteProvider);
            var section = provider.Sections[sectionNumber];
            var page = await provider.GetSectionPageParamsAsync(section, default);
            var filter = new SectionPageFilter(page!);

            var resultItems = await provider.GetFullResult(filter).Take(40).ToListAsync();

            Assert.That(resultItems, Is.Not.Empty, "Zero items count on page");
            Assert.That(resultItems.Count, Is.EqualTo(40), "Missing items");
            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            Assert.That(resultItems.All(i => i.Site == provider.Site), Is.True, "Item site is invalid");
            Assert.That(resultItems.All(i => i.Section.Modifier.HasFlag(section.Modifier)), Is.True, "Item section is not match");
            Assert.That(resultItems.All(i => !i.Poster.Any() || i.Poster.All(p => p.Value.IsAbsoluteUri)), Is.True,
                "Item poster image is not absolute uri");
            Assert.That(resultItems.Any(i => i.Poster.Any()), Is.True, "No items with poster");
            Assert.That(resultItems.Any(i => i.Link != null), Is.True, "Item link is null");
            Assert.That(resultItems.Any(i => i.Details.Year != null), Is.True, "Item year is null");
            Assert.That(resultItems.Any(i => !string.IsNullOrWhiteSpace(i.Title)), Is.True, "Item title is empty");
        }

        [Test]
        [Timeout(120000)]
        public async Task RezkaItemProvider_GetHomePageAsync()
        {
            var provider = new RezkaItemProvider(new RezkaSiteProvider(Test.ProviderConfigService));
            var homePage = await provider.GetHomePageModelAsync(default);
            Assert.That(homePage, Is.Not.Null);

            var groups = homePage!.HomeItems.ToList();
            if (homePage!.TopItems.Any())
            {
                groups.Add(homePage.TopItems.GroupBy(_ => homePage.TopItemsCaption).First()!);
            }

            foreach (var items in groups)
            {
                Assert.That(items.Key, Is.Not.Null & Is.Not.Empty);

                Assert.That(items, Is.Not.Empty, "Zero items count on page");

                CollectionAssert.AllItemsAreNotNull(items, "Some item is null");
                CollectionAssert.AllItemsAreUnique(items, "Items is not unique");

                foreach (var item in items)
                {
                    Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                    Assert.That(item.Link, Is.Not.Null, "Item link is null");
                    Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                    Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                    Assert.That(item.Title, Is.Not.Null & Is.Not.Empty, "Item title is empty");
                }
            }
        }
    }
}

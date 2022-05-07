namespace FSClient.Providers.Test.ExFS
{
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemProvider)]
    public class ExFSItemProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ExFSSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase(1)]
        [TestCase(2)]
        [Timeout(120000)]
        public async Task ExFSItemProvider_GetItemsAsync(int sectionNumber)
        {
            var provider = new ExFSItemProvider(new ExFSSiteProvider(Test.ProviderConfigService));
            var section = provider.Sections[sectionNumber];
            var page = await provider.GetSectionPageParamsAsync(provider.Sections[sectionNumber], default);
            var filter = new SectionPageFilter(page!);

            var items = await provider.GetFullResult(filter).Take(45).ToListAsync();

            Assert.That(items, Is.Not.Empty, "Zero items count on page");
            Assert.That(items.Count, Is.EqualTo(45), "Missing items");

            CollectionAssert.AllItemsAreNotNull(items, "Some item is null");
            CollectionAssert.AllItemsAreUnique(items, "Items is not unique");

            foreach (var item in items)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(item.Section, Is.EqualTo(section), "Item section is invalid");
            }
            if (section.Modifier.HasFlag(SectionModifiers.Serial)
                && items.All(i => !i.Details.Status.CurrentEpisode.HasValue))
            {
                Assert.Fail("Item episode is null");
            }
            if (section.Modifier.HasFlag(SectionModifiers.Serial)
                && items.All(i => !i.Details.Status.CurrentSeason.HasValue))
            {
                Assert.Fail("Item season is null");
            }
        }

        [Test]
        [Timeout(120000)]
        public async Task ExFSItemProvider_GetHomePageAsync()
        {
            var provider = new ExFSItemProvider(new ExFSSiteProvider(Test.ProviderConfigService));
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

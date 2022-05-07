namespace FSClient.Providers.Test.Kodik
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class KodikSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new KodikSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Друзья", false)]
        [TestCase("Грабитель", true)]
        [Timeout(10000)]
        public async Task KodikSearchProvider_GetFullResultAsync(string query, bool isSerial)
        {
            var provider = new KodikSearchProvider(new KodikSiteProvider(Test.ProviderConfigService));

            var section = provider.Sections.First(s => s.Modifier.HasFlag(SectionModifiers.Serial) == isSerial);

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
                Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
                Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
                Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.EqualTo(isSerial), "Item section is invalid");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(string.IsNullOrWhiteSpace(item.Details.TitleOrigin), Is.False, "Item origin title is empty");
            }
            if (resultItems.All(i => string.IsNullOrWhiteSpace(i.Details.TitleOrigin)))
            {
                Assert.Inconclusive("Every item's origin title is null or white space");
            }

            if (!resultItems.Any(i =>
                i.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var id)
                && id?.ToIntOrNull().HasValue == true))
            {
                Assert.Inconclusive("No one item has kp id");
            }
        }

        //[TestCase("464963", true)]
        [TestCase("933579", false)]
        [Timeout(10000)]
        public async Task KodikSearchProvider_FindSimilarByKpIdAsync(string kpId, bool isSerial)
        {
            var sourceItem = new ItemInfo(Site.Any, "")
            {
                Details =
                {
                    LinkedIds =
                    {
                        [Sites.Kinopoisk] = kpId
                    }
                },
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
            };

            var provider = new KodikSearchProvider(new KodikSiteProvider(Test.ProviderConfigService));

            var resultItems = (await provider
                .FindSimilarAsync(sourceItem, CancellationToken.None)
                .ConfigureAwait(false))
                .ToList();

            Assert.That(resultItems.Count > 0, Is.True, "Zero items count");
            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
                Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
                Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.EqualTo(isSerial), "Item section is invalid");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(string.IsNullOrWhiteSpace(item.Details.TitleOrigin), Is.False, "Item origin title is empty");
            }
            if (resultItems.All(i => i.Title == i.Details.TitleOrigin))
            {
                Assert.Inconclusive("Every item's origin title is equal to title");
            }
        }
    }
}

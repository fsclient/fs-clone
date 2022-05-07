namespace FSClient.Providers.Test.Collaps
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
    [Ignore("Broken")]
    public class CollapsSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new CollapsSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Game of thrones", true)]
        [TestCase("Форсаж", false)]
        [Timeout(10000)]
        public async Task CollapsSearchProvider_GetFullResultAsync(string query, bool isSerial)
        {
            var provider = new CollapsSearchProvider(new CollapsSiteProvider(Test.ProviderConfigService));

            var page = await provider.GetSearchPageParamsAsync(Section.CreateDefault(isSerial
                ? SectionModifiers.Serial
                : SectionModifiers.Film), default);
            var filter = new SearchPageFilter(page!, query);

            var resultItems = await provider.GetFullResult(filter).Take(30).ToListAsync();
            Assert.That(resultItems, Is.Not.Empty, "Zero items count on page");

            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
                Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.EqualTo(isSerial), "Item section is invalid");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
            }
            if (!resultItems.Any(i =>
                    i.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var id)
                    && id?.ToIntOrNull().HasValue == true))
            {
                Assert.Inconclusive("No one item has kp id");
            }
        }

        [TestCase("77044", true)]
        [TestCase("666", false)]
        [Timeout(10000)]
        public async Task CollapsSearchProvider_FindSimilarByKpIdAsync(string kpId, bool isSerial)
        {
            var sourceItem = new ItemInfo(Site.Any, "kp" + kpId)
            {
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film),
                Details =
                {
                    LinkedIds =
                    {
                        [Sites.Kinopoisk] = kpId
                    }
                }
            };

            var provider = new CollapsSearchProvider(new CollapsSiteProvider(Test.ProviderConfigService));

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
                Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
                Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.EqualTo(isSerial), "Item section is invalid");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
            }
            if (!resultItems.Any(i =>
                    i.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var id)
                    && id?.ToIntOrNull().HasValue == true))
            {
                Assert.Inconclusive("No one item has kp id");
            }
        }
    }
}

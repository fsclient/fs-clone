namespace FSClient.Providers.Test.TMDb
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ReviewProvider)]
    public class TMDbReviewProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub()).CheckIsAvailableAsync();
        }

        [TestCase("216015", false)]
        [TestCase("1402", true)]
        [Timeout(10000)]
        public async Task TMDbReviewsProvider_GetReviews(string id, bool isSerial)
        {
            var siteProvider = new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub());
            var itemInfo = new ItemInfo(siteProvider.Site, id)
            {
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
            };

            var provider = new TMDbReviewProvider(siteProvider);

            var reviews = await provider.GetReviews(itemInfo)
                .Take(20)
                .ToListAsync()
                .ConfigureAwait(false);

            Assert.That(reviews.Count > 0, Is.True, "Zero reviews count");
            CollectionAssert.AllItemsAreNotNull(reviews, "Some item is null");
            CollectionAssert.AllItemsAreUnique(reviews, "Items in not unique");
            foreach (var (review, _) in reviews)
            {
                Assert.That(string.IsNullOrWhiteSpace(review.Id), Is.False, "Id is null or empty");
                Assert.That(review.Site, Is.EqualTo(provider.Site), $"Item site is invalid - {review.Id}");
                Assert.That(string.IsNullOrWhiteSpace(review.Reviewer), Is.False, $"Reviewer is null or empty - {review.Id}");
                Assert.That(string.IsNullOrWhiteSpace(review.Description), Is.False, $"Description is null or empty - {review.Id}");

                // Not implemented:
                // Assert.IsNotNull(review.Date, $"Date is null - {review.Id}");
                // Assert.IsFalse(review.Date == default(DateTime), $"Date is default - {review.Id}");
                // Assert.IsNotNull(review.Avatar, $"Avatar is null - {review.Id}");
                // Assert.IsTrue(review.Avatar.IsAbsoluteUri, $"Avatar is not absolute uri - {review.Id}");
            }
        }
    }
}

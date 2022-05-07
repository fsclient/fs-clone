namespace FSClient.Shared.Test.Helpers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;

    using NUnit.Framework;

    [TestFixture]
    public class EnumerableHelperTest
    {
        [Test]
        public async Task Should_Run_WhenAllAsync_In_Parallel()
        {
            var order = 0;
            var inputs = new[] { 10, 20, 100, 9, 7, 0, 11, 16 };
            var startResults = new List<int>();
            var endResults = new List<int>();

            var expected = inputs.Where(res => res < 100).ToArray();
            await inputs
                .ToAsyncEnumerable()
                .Where(res => res < 100)
                .WhenAll(async (res, _) =>
                {
                    startResults.Add(res);
                    await Task.Delay(res, _);
                    return (res, order: order++);
                })
                .OrderBy(t => t.order)
                .ForEachAsync(t => endResults.Add(t.res));

            CollectionAssert.AreEqual(expected, startResults, "Start");
            CollectionAssert.AreEquivalent(expected, startResults, "Start");

            CollectionAssert.AreNotEqual(expected, endResults, "End");
            CollectionAssert.AreEquivalent(expected, endResults, "End");
        }
    }
}

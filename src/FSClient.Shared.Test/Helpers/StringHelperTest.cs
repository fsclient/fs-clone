namespace FSClient.Shared.Test.Helpers
{
    using FSClient.Shared.Helpers;

    using NUnit.Framework;

    [TestFixture]
    public class StringHelperTest
    {
        [TestCase("Рус.Англ. субтитры", 3153933791u)]
        [TestCase("Lostfilm", 3595399602u)]
        [TestCase("AniDub", 373089689u)]
        [TestCase("УкраїнськіSubs", 3279423489u)]
        public void Should_Produce_Deterministic_HashCode(string input, uint expected)
        {
            var result = StringHelper.GetDeterministicHashCode(input);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}

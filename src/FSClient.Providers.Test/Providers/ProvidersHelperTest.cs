namespace FSClient.Shared.Test.Helpers
{
    using System.Linq;

    using FSClient.Providers;

    using NUnit.Framework;

    [TestFixture]
    public class ProvidersHelperTest
    {
        [Test]
        public void Should_Parse_PlayerJs_KeyValue_Pairs()
        {
            const string input = "[360p]https://link/360 or https://link/360_2, [720p]https://link/720/, [1080p] with space";

            var results = ProviderHelper.ParsePlayerJsKeyValuePairs(input).ToList();

            Assert.That(results.Count, Is.EqualTo(4));

            Assert.That(results[0].key, Is.EqualTo("360p"));
            Assert.That(results[0].subKey, Is.Null);
            Assert.That(results[0].value, Is.EqualTo("https://link/360"));

            Assert.That(results[1].key, Is.EqualTo("360p"));
            Assert.That(results[1].subKey, Is.Null);
            Assert.That(results[1].value, Is.EqualTo("https://link/360_2"));

            Assert.That(results[2].key, Is.EqualTo("720p"));
            Assert.That(results[2].subKey, Is.Null);
            Assert.That(results[2].value, Is.EqualTo("https://link/720/"));

            Assert.That(results[3].key, Is.EqualTo("1080p"));
            Assert.That(results[3].subKey, Is.Null);
            Assert.That(results[3].value, Is.EqualTo(" with space"));
        }

        [Test]
        public void Should_Parse_PlayerJs_KeyValue_Pairs_With_Subkey()
        {
            const string input = "[360p]{Dub}https://link/360 or https://link/360_2;{Sub}https://link/360/sub or https://link/360_2/sub,[720p]{Dub}https://link/720,";

            var results = ProviderHelper.ParsePlayerJsKeyValuePairs(input).ToList();

            Assert.That(results.Count, Is.EqualTo(5));

            Assert.That(results[0].key, Is.EqualTo("360p"));
            Assert.That(results[0].subKey, Is.EqualTo("Dub"));
            Assert.That(results[0].value, Is.EqualTo("https://link/360"));

            Assert.That(results[1].key, Is.EqualTo("360p"));
            Assert.That(results[1].subKey, Is.EqualTo("Dub"));
            Assert.That(results[1].value, Is.EqualTo("https://link/360_2"));

            Assert.That(results[2].key, Is.EqualTo("360p"));
            Assert.That(results[2].subKey, Is.EqualTo("Sub"));
            Assert.That(results[2].value, Is.EqualTo("https://link/360/sub"));

            Assert.That(results[3].key, Is.EqualTo("360p"));
            Assert.That(results[3].subKey, Is.EqualTo("Sub"));
            Assert.That(results[3].value, Is.EqualTo("https://link/360_2/sub"));

            Assert.That(results[4].key, Is.EqualTo("720p"));
            Assert.That(results[4].subKey, Is.EqualTo("Dub"));
            Assert.That(results[4].value, Is.EqualTo("https://link/720"));
        }

        [Test]
        public void Should_Parse_PlayerJs_KeyValue_Pairs_Single_Value()
        {
            const string input = "https://link/360";

            var results = ProviderHelper.ParsePlayerJsKeyValuePairs(input).ToList();

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].key, Is.EqualTo(string.Empty));
            Assert.That(results[0].value, Is.EqualTo("https://link/360"));
        }
    }
}

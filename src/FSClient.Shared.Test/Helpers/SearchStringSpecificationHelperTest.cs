namespace FSClient.Shared.Test.Helpers
{
    using FSClient.Shared.Helpers;

    using NUnit.Framework;

    [TestFixture]
    public class SearchStringSpecificationHelperTest
    {
        [Test]
        public void Should_Prepare_Filters_Started_With_Concrete()
        {
            const string userInput = "\"Concrete start\"\"another\" non-concrete, another 5";
            var prepared = SearchStringSpecificationHelper.PrepareFilters(userInput);

            Assert.That(prepared[0].isConcrete, Is.True);
            Assert.That(prepared[0].text, Is.EqualTo("Concrete start"));

            Assert.That(prepared[1].isConcrete, Is.True);
            Assert.That(prepared[1].text, Is.EqualTo("another"));

            Assert.That(prepared[2].isConcrete, Is.False);
            Assert.That(prepared[2].text, Is.EqualTo("nonconcrete"));

            Assert.That(prepared[3].isConcrete, Is.False);
            Assert.That(prepared[3].text, Is.EqualTo("another"));

            Assert.That(prepared[4].isConcrete, Is.False);
            Assert.That(prepared[4].text, Is.EqualTo("5"));
        }

        [Test]
        public void Should_Prepare_Filters_Ended_With_Concrete()
        {
            const string userInput = "non-concrete \"concrete\"";
            var prepared = SearchStringSpecificationHelper.PrepareFilters(userInput);

            Assert.That(prepared[0].isConcrete, Is.False);
            Assert.That(prepared[0].text, Is.EqualTo("nonconcrete"));

            Assert.That(prepared[1].isConcrete, Is.True);
            Assert.That(prepared[1].text, Is.EqualTo("concrete"));
        }

        [TestCase("Hello World", "llo Wo")]
        [TestCase("Hello World", "Hello")]
        [TestCase("Hello World", "o world")]
        public void Should_Check_Concrete_Plain_String(string input, string filter)
        {
            var result = SearchStringSpecificationHelper.CheckPlainString(input, filter, true);
            Assert.That(result, Is.True);
        }

        [TestCase("Hello 1 World", "llo Wo")]
        [TestCase("Hello_World", "llo Wo")]
        [TestCase("Hello World", "lloWo")]
        public void Should_Not_Check_Concrete_Plain_String(string input, string filter)
        {
            var result = SearchStringSpecificationHelper.CheckPlainString(input, filter, true);
            Assert.That(result, Is.False);
        }

        [TestCase("Hello World", "lloWo")]
        [TestCase("Hello World", "Hello")]
        [TestCase("Hello World", "oworld")]
        [TestCase("HelloWorld", "lloWo")]
        [TestCase("Hello+World", "Hello")]
        [TestCase("Hello_World", "oworld")]
        public void Should_Check_Non_Concrete_Plain_String(string input, string filter)
        {
            var result = SearchStringSpecificationHelper.CheckPlainString(input, filter, false);
            Assert.That(result, Is.True);
        }

        [TestCase("Hello World", "nowhere")]
        [TestCase("Hello World", "hello1")]
        public void Should_Not_Check_Non_Concrete_Plain_String(string input, string filter)
        {
            var result = SearchStringSpecificationHelper.CheckPlainString(input, filter, false);
            Assert.That(result, Is.False);
        }
    }
}

namespace FSClient.Shared.Test.Helpers
{
    using System;

    using FSClient.Shared.Helpers;

    using NUnit.Framework;

    [TestFixture]
    public class UriHelperTest
    {
        [TestCase("https://www.amazon.co.uk", "amazon.co.uk")]
        [TestCase("https://google.com", "google.com")]
        [TestCase("https://translate.google.com", "google.com")]
        [TestCase("https://com", "com")]
        [TestCase("https://co.uk", "co.uk")]
        // Ignored:
        //[TestCase("https://random.name.et", "random.name.et")]
        //[TestCase("https://www.random.name.et", "random.name.et")]
        public void Should_Get_Root_Domain(string input, string expected)
        {
            var output = UriHelper.GetRootDomain(new Uri(input));
            Assert.That(output, Is.EqualTo(expected));
        }
    }
}

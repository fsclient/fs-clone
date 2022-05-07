namespace FSClient.Shared.Test.Helpers
{
    using System.Linq;

    using FSClient.Shared.Helpers;

    using NUnit.Framework;

    [TestFixture]
    public class LocalizationHelperTest
    {
        [TestCase("фыва ру фыва", LocalizationHelper.RuLang)]
        [TestCase("русский", LocalizationHelper.RuLang)]
        [TestCase("фыва rus фыва", LocalizationHelper.RuLang)]
        [TestCase("ru", LocalizationHelper.RuLang)]
        [TestCase("фыва укр фыва", LocalizationHelper.UaLang)]
        [TestCase("украинский", LocalizationHelper.UaLang)]
        [TestCase("фыва ua фыва", LocalizationHelper.UaLang)]
        [TestCase("ukr", LocalizationHelper.UaLang)]
        [TestCase("фыва анг фыва", LocalizationHelper.EnLang)]
        [TestCase("английский", LocalizationHelper.EnLang)]
        [TestCase("фыва en фыва", LocalizationHelper.EnLang)]
        [TestCase("eng", LocalizationHelper.EnLang)]
        [TestCase("японский", LocalizationHelper.JpLang)]
        [TestCase("japanese", LocalizationHelper.JpLang)]
        [TestCase("jp", LocalizationHelper.JpLang)]
        public void Should_Detect_Correct_Language(string input, string expected)
        {
            var results = LocalizationHelper.DetectLanguageNames(input);
            var output = results.Single();
            Assert.That(output, Is.EqualTo(expected));
        }
    }
}

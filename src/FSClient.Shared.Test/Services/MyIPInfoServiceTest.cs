namespace FSClient.Shared.Test.Services
{
    using System.Globalization;
    using System.Net;
    using System.Threading.Tasks;

    using FSClient.Shared.Services;

    using NUnit.Framework;

    [TestFixture]
    public class MyIPInfoServiceTest
    {
        [Test]
        public async Task Should_Get_Current_User_IPInfoAsync()
        {
            IIPInfoService ipInfoService = new MyIPInfoService();
            var userInfo = await ipInfoService.GetCurrentUserIPInfoAsync(default);
            Assert.That(userInfo?.IP, Is.Not.Null);
            Assert.That(userInfo?.Country, Is.Not.Null);
            Assert.That(userInfo?.CC, Is.Not.Null);
            Assert.That(IPAddress.TryParse(userInfo!.IP, out _), Is.True);
            Assert.That(CultureInfo.GetCultureInfo(userInfo!.CC!), Is.Not.Null);
        }
    }
}

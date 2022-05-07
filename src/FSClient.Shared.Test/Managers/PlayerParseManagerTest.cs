namespace FSClient.Shared.Test.Managers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class PlayerParseManagerTest
    {
        [Timeout(20000)]
        [TestCase("http://clips.vorwaerts-gmbh.de/big_buck_bunny.mp4")]
        [TestCase("https://bitdash-a.akamaihd.net/content/MI201109210084_1/m3u8s/f08e80da-bf1d-4e3d-8899-f0f6155f6efa.m3u8")]
        public async Task PlayerParseHelpers_GetVideosFromElse(string url)
        {
            var manager = new PlayerParseManager(Array.Empty<IPlayerParseProvider>(), null!, Test.Logger);
            var file = await manager.ParseFromUriAsync(new Uri(url), Site.Any, CancellationToken.None);

            Assert.That(file, Is.Not.Null);
            // await ProvidersTestHelpers.CheckVideosAsync(file!.Videos, file);
        }

        [Timeout(20000)]
        [TestCase("http://terrillthompson.com/tests/youtube.html")]
        public async Task PlayerParseHelpers_GetVideosFromPageWithIFrame(string url)
        {
            var mockSite = Site.GetOrCreate("youtube", "YouTube");
            var youtubePlayerParseProviderMock = new Mock<IPlayerParseProvider>();
            youtubePlayerParseProviderMock.Setup(p => p.CanOpenFromLinkOrHostingName(It.IsAny<Uri>()))
                .Returns<Uri>((link) =>
                    link.Host.Contains("youtu", StringComparison.OrdinalIgnoreCase));
            youtubePlayerParseProviderMock.Setup(p => p.Site)
                .Returns(() => mockSite);
            youtubePlayerParseProviderMock.Setup(p => p.ParseFromUriAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .Returns<Uri, CancellationToken>((link, ct) =>
                {
                    var file = new File(mockSite, "youtube");
                    file.SetVideos(new Video(link));
                    return Task.FromResult<File?>(file);
                });

            var userManagerMock = new Mock<IUserManager>();
            userManagerMock.Setup(m => m.CheckRequirementsAsync(mockSite, It.IsAny<ProviderRequirements>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<bool>(true));

            var manager = new PlayerParseManager(new[] { youtubePlayerParseProviderMock.Object }, userManagerMock.Object, Test.Logger);
            var file = await manager.ParseFromUriAsync(new Uri(url), mockSite, CancellationToken.None);

            Assert.That(file, Is.Not.Null);
            Assert.That(file!.Site, Is.EqualTo(mockSite));
        }
    }
}

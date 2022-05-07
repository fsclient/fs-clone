namespace FSClient.Shared.Test.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class TrackRestoreFromSettingServiceTest
    {
        private readonly Uri specialUri = new Uri("schema://special#hash");
        private readonly Uri blankUri = new Uri("schema://blank#hash");

        [TestCase(LocalizationHelper.RuLang)]
        [TestCase(LocalizationHelper.EnLang)]
        [TestCase(null)]
        public void Should_Save_And_Restore_From_State_Language(string? lang)
        {
            var subtitle = new SubtitleTrack(lang, specialUri);
            var tracks = new TrackCollection<SubtitleTrack>(GenerateTracks(lang => new SubtitleTrack(lang, blankUri))
                .Where(t => t.Language != lang)
                .Concat(new[] { subtitle }));

            var settingsMock = GenerateSettingService();
            var service = new TrackRestoreFromSettingService(settingsMock.Object);

            service.Save(subtitle, null);
            var restoredTrack = service.Restore(tracks, null);

            Assert.That(restoredTrack, Is.EqualTo(subtitle));

            settingsMock.Verify(
                s => s.SetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<SettingStrategy>()),
                Times.AtLeastOnce());
            settingsMock.Verify(
                s => s.GetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<SettingStrategy>()),
                Times.AtLeastOnce());
            settingsMock.Verify(
                s => s.SetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SettingStrategy>()),
                Times.AtLeastOnce());
            settingsMock.VerifyNoOtherCalls();
        }

        [TestCase(LocalizationHelper.RuLang)]
        [TestCase(LocalizationHelper.EnLang)]
        [TestCase(null)]
        public void Should_Save_And_Restore_From_State_Language_SameLanguages(string? lang)
        {
            var file = new File(Site.Any, "-")
            {
                ItemInfo = new ItemInfo(Site.Any, "exact")
            };

            var subtitle = new SubtitleTrack(lang, specialUri);
            var tracks = new TrackCollection<SubtitleTrack>(GenerateTracks(lang => new SubtitleTrack(lang, blankUri)))
            {
                subtitle
            };

            var settingsMock = GenerateSettingService();
            var service = new TrackRestoreFromSettingService(settingsMock.Object);

            service.Save(subtitle, file);
            var restoredTrack = service.Restore(tracks, file);

            Assert.That(restoredTrack, Is.EqualTo(subtitle));

            settingsMock.Verify(
                s => s.SetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<SettingStrategy>()),
                Times.AtLeastOnce());
            settingsMock.Verify(
                s => s.GetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<SettingStrategy>()),
                Times.AtLeastOnce());
            settingsMock.Verify(
                s => s.SetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SettingStrategy>()),
                Times.AtLeastOnce());
            settingsMock.Verify(
                s => s.GetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SettingStrategy>()),
                Times.AtLeastOnce());
            settingsMock.VerifyNoOtherCalls();
        }

        [Test]
        public void Should_Return_Null_On_Track_Not_Found()
        {
            var tracks = new TrackCollection<SubtitleTrack>(GenerateTracks(lang => new SubtitleTrack(lang, blankUri)));

            var settingsMock = GenerateSettingService();
            var service = new TrackRestoreFromSettingService(settingsMock.Object);

            var restoredTrack = service.Restore(tracks, null);

            Assert.That(restoredTrack, Is.Null);

            settingsMock.Verify(
                s => s.GetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<SettingStrategy>()),
                Times.Exactly(1));
            settingsMock.VerifyNoOtherCalls();
        }

        [Test]
        public void Should_Delete_On_Null_Track()
        {
            var settingsMock = GenerateSettingService();
            var service = new TrackRestoreFromSettingService(settingsMock.Object);

            service.Save<SubtitleTrack>(null, null);

            settingsMock.Verify(
                s => s.DeleteSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<SettingStrategy>()),
                Times.AtLeastOnce());
            settingsMock.VerifyNoOtherCalls();
        }

        private static IEnumerable<TTrack> GenerateTracks<TTrack>(Func<string?, TTrack> factory)
        {
            yield return factory(LocalizationHelper.RuLang);
            yield return factory(LocalizationHelper.EnLang);
            yield return factory(null); // NULL for unknown language
            yield return factory(LocalizationHelper.JpLang);
            yield return factory(LocalizationHelper.UaLang);
        }

        private static Mock<ISettingService> GenerateSettingService()
        {
            var settingsMock = new Mock<ISettingService>();

            var dictionary = new Dictionary<string, object?>();

            settingsMock.Setup(s => s.SetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<SettingStrategy>()))
                .Returns(true)
                .Callback<string, string, string?, SettingStrategy>((_, key, value, __) => dictionary[key] = value);
            settingsMock.Setup(s => s.GetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<SettingStrategy>()))
                .Returns<string, string, string?, SettingStrategy>((_, key, otherwise, __) => dictionary.TryGetValue(key, out var value) ? value?.ToString() : otherwise);

            settingsMock.Setup(s => s.SetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SettingStrategy>()))
                .Returns(true)
                .Callback<string, string, int, SettingStrategy>((_, key, value, __) => dictionary[key] = value);
            settingsMock.Setup(s => s.GetSetting(Settings.StateSettingsContainer, It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SettingStrategy>()))
                .Returns<string, string, int, SettingStrategy>((_, key, otherwise, __) => dictionary.TryGetValue(key, out var value) ? (int)value! : otherwise);

            return settingsMock;
        }
    }
}

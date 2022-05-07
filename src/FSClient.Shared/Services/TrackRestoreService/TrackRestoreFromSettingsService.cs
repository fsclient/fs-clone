namespace FSClient.Shared.Services
{
    using System.Linq;

    using FSClient.Shared.Models;

    public class TrackRestoreFromSettingService : ITrackRestoreService
    {
        private const string CacheLangSuffix = "_Lang";
        private const string CacheIndexSuffix = "_Index";
        private const string CacheItemIdSuffix = "_ItemId";

        private const string UnknownLanguage = "Unknown";

        private readonly ISettingService settingService;

        public TrackRestoreFromSettingService(ISettingService settingService)
        {
            this.settingService = settingService;
        }

        public TTrack? Restore<TTrack>(TrackCollection<TTrack> tracks, File? file) where TTrack : class, ITrack
        {
            var langKey = PrepareKey<TTrack>(CacheLangSuffix);
            var lang = settingService.GetSetting(Settings.StateSettingsContainer, langKey, null, SettingStrategy.Local);

            if (lang == null)
            {
                return default;
            }

            var exactLangTracks = tracks
                .Where(track => lang == UnknownLanguage
                    ? track.Language == null
                    : track.Language == lang)
                .ToArray();
            if (exactLangTracks.Length == 1)
            {
                return exactLangTracks[0];
            }

            var indexKey = PrepareKey<TTrack>(CacheIndexSuffix);
            var itemIdKey = PrepareKey<TTrack>(CacheItemIdSuffix);
            var index = settingService.GetSetting(Settings.StateSettingsContainer, indexKey, -1, SettingStrategy.Local);
            var itemId = settingService.GetSetting(Settings.StateSettingsContainer, itemIdKey, null, SettingStrategy.Local);

            if (file?.ItemInfo?.SiteId == itemId
                && index >= 0)
            {
                return exactLangTracks.FirstOrDefault(t => t.Index == index);
            }

            return exactLangTracks.FirstOrDefault();
        }

        public void Save<TTrack>(TTrack? track, File? file) where TTrack : class, ITrack
        {
            var langKey = PrepareKey<TTrack>(CacheLangSuffix);
            var indexKey = PrepareKey<TTrack>(CacheIndexSuffix);
            var itemIdKey = PrepareKey<TTrack>(CacheItemIdSuffix);

            if (track == null)
            {
                settingService.DeleteSetting(Settings.StateSettingsContainer, langKey, SettingStrategy.Local);
                settingService.DeleteSetting(Settings.StateSettingsContainer, indexKey, SettingStrategy.Local);
                settingService.DeleteSetting(Settings.StateSettingsContainer, itemIdKey, SettingStrategy.Local);
            }
            else
            {
                settingService.SetSetting(Settings.StateSettingsContainer, langKey,
                    track.Language ?? UnknownLanguage);
                settingService.SetSetting(Settings.StateSettingsContainer, indexKey,
                    track.Index);
                settingService.SetSetting(Settings.StateSettingsContainer, itemIdKey,
                    file?.ItemInfo?.SiteId);
            }
        }

        private static string PrepareKey<TTrack>(string suffix)
            where TTrack : ITrack
        {
            return $"{typeof(TTrack).Name}_{suffix}";
        }
    }
}

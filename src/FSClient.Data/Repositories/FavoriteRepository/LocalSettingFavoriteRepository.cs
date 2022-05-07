namespace FSClient.Data.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;

    public class LocalSettingFavoriteRepository : IFavoriteRepository
    {
        internal readonly Lazy<Dictionary<FavoriteListKind, List<FavoriteItem>>> cacheLazy;

        private readonly ISettingService settingService;

        public LocalSettingFavoriteRepository(
            ISettingService settingService)
        {
            this.settingService = settingService;

            cacheLazy = new Lazy<Dictionary<FavoriteListKind, List<FavoriteItem>>>(() =>
            {
                var items = GetLocalFavorite();

                var cache = new[] { FavoriteListKind.Favorites, FavoriteListKind.ForLater, FavoriteListKind.InProcess, FavoriteListKind.Finished }
                    .ToDictionary(
                        t => t,
                        t => items
                            .Where(tuple => tuple.Type.HasFlag(GetFlagTypeFromListKind(t)))
                            .Select(tuple => new FavoriteItem(tuple.Item, t))
                            .ToList());
                return cache;
            });
        }

        public async ValueTask<bool> DeleteAsync(string id)
        {
            var cache = cacheLazy.Value;

            var item = cache.Values.SelectMany(l => l).FirstOrDefault(f => f.Key == id);
            if (item != null)
            {
                return await DeleteManyAsync(new[] { item }).ConfigureAwait(false) > 0;
            }
            return false;
        }

        public ValueTask<int> DeleteManyAsync(IEnumerable<FavoriteItem> items)
        {
            var cache = cacheLazy.Value;
            var deletedCount = 0;

            foreach (var item in items)
            {
                var kind = GetFlagTypeFromListKind(item.ListKind);

                var roamingType = GetFavoriteTypeFlagsFromSettingStrategy(item.ItemInfo, SettingStrategy.Roaming);
                var localType = roamingType ?? GetFavoriteTypeFlagsFromSettingStrategy(item.ItemInfo, SettingStrategy.Local) ?? FlagsFavoriteTypes.None;
                localType &= ~kind;
                SetLocalInfo(item.ItemInfo, localType, SettingStrategy.Local);

                if (roamingType.HasValue)
                {
                    SetLocalInfo(item.ItemInfo, localType, SettingStrategy.Roaming);
                }

                if (cache[item.ListKind].Remove(item))
                {
                    deletedCount++;
                }
            }

            return new ValueTask<int>(deletedCount);
        }

        public ValueTask<FavoriteItem?> FindAsync(Expression<Func<FavoriteItem, bool>> predicate)
        {
            return GetAll().FirstOrDefaultAsync(predicate.Compile())!;
        }

        public IAsyncEnumerable<FavoriteItem> GetAll()
        {
            return cacheLazy.Value.Values
                .SelectMany(l => l)
                .ToArray()
                .ToAsyncEnumerable();
        }

        public IAsyncEnumerable<FavoriteItem> GetAllByFavoriteListKind(FavoriteListKind listKind)
        {
            return cacheLazy.Value[listKind]
                .ToArray()
                .ToAsyncEnumerable();
        }

        public ValueTask<FavoriteItem?> GetAsync(string id)
        {
            return GetAll().FirstOrDefaultAsync(f => f.Key == id)!;
        }

        public IAsyncEnumerable<FavoriteItem> GetFavoritesByItems(IEnumerable<string> itemIds)
        {
            return cacheLazy.Value.Values
                .SelectMany(l => l)
                .Where(i => itemIds.Contains(i.ItemInfo.Key))
                .ToArray()
                .ToAsyncEnumerable();
        }

        public ValueTask<int> UpsertManyAsync(IEnumerable<FavoriteItem> items)
        {
            var cache = cacheLazy.Value;
            var upsertedCount = 0;

            foreach (var item in items)
            {
                var kind = GetFlagTypeFromListKind(item.ListKind);
                var roamingType = GetFavoriteTypeFlagsFromSettingStrategy(item.ItemInfo, SettingStrategy.Roaming);
                var localType = roamingType ?? GetFavoriteTypeFlagsFromSettingStrategy(item.ItemInfo, SettingStrategy.Local) ?? FlagsFavoriteTypes.None;
                localType |= kind;
                SetLocalInfo(item.ItemInfo, localType, SettingStrategy.Local);

                if (roamingType.HasValue)
                {
                    SetLocalInfo(item.ItemInfo, localType, SettingStrategy.Roaming);
                }

                if (!cache[item.ListKind].Contains(item))
                {
                    upsertedCount++;
                    cache[item.ListKind].Add(item);
                }
            }

            return new ValueTask<int>(upsertedCount);
        }

        private IEnumerable<(ItemInfo Item, FlagsFavoriteTypes Type)> GetLocalFavorite()
        {
            var rawSettings = settingService
                .GetAllContainers(SettingStrategy.Local)
                .Select(container => (container, strategy: SettingStrategy.Local));

            rawSettings = rawSettings.Concat(settingService
                .GetAllContainers(SettingStrategy.Roaming)
                .Select(container => (container, strategy: SettingStrategy.Roaming)));

            return rawSettings
                .Where(t => settingService.SettingExists(t.container, "FavType", t.strategy))
                .GroupBy(t => t.container)
                .Select(g => g.OrderBy(v => v.strategy == SettingStrategy.Roaming).First())
                .Select(t => (t.container, t.strategy, settingService.GetAllRawSettings(t.container, t.strategy)))
                .Select(tuple =>
                {
                    var (id, strategy, value) = tuple;
                    var itemFavType = (FlagsFavoriteTypes?)(byte?)value["FavType"] ?? FlagsFavoriteTypes.None;

                    return (
                        item: SettingToItem(value, id),
                        kind: itemFavType
                    );
                })
                .Where(tuple => tuple.item != null! && tuple.item.Site != Site.Any)
                .ToArray()!;
        }

        private FlagsFavoriteTypes? GetFavoriteTypeFlagsFromSettingStrategy(ItemInfo item, SettingStrategy settingStrategy)
        {
            var id = GenerateFavItemId(item);

            var setting = settingService.GetSetting(id, "FavType", (byte?)null, settingStrategy);
            return (FlagsFavoriteTypes?)setting;
        }

        private bool SetLocalInfo(ItemInfo item, FlagsFavoriteTypes favType, SettingStrategy settingsStrategy)
        {
            if (item?.Link == null)
            {
                return false;
            }

            var id = GenerateFavItemId(item);

            if (favType == FlagsFavoriteTypes.None)
            {
                settingService.Clear(id, settingsStrategy);
                settingService.DeleteSetting(settingService.RootContainer, id, settingsStrategy);
                return true;
            }

            var posterUri = item.Poster.GetOrBigger(ImageSize.Preview);
            if (posterUri != null
                && item.Link.IsAbsoluteUri == true
                && !posterUri.IsAbsoluteUri)
            {
                posterUri = new Uri(item.Link, posterUri);
            }

            if (!settingService.SetSetting(id, "Id", item.SiteId, settingsStrategy))
            {
                return false;
            }
            settingService.SetSetting(id, "Title", item.Title, settingsStrategy);
            settingService.SetSetting(id, "Link", item.Link.GetPath(), settingsStrategy);
            settingService.SetSetting(id, "Poster", posterUri?.ToString(), settingsStrategy);
            settingService.SetSetting(id, "Provider", item.Site.Value, settingsStrategy);

            if (item.Section.Modifier.HasFlag(SectionModifiers.Serial))
            {
                settingService.SetSetting(id, "IsSerial", true, settingsStrategy);
            }

            if (item.Section.Modifier.HasFlag(SectionModifiers.Cartoon))
            {
                settingService.SetSetting(id, "IsCartoon", true, settingsStrategy);
            }

            settingService.SetSetting(id, "FavType", (byte)favType, settingsStrategy);

            return true;
        }

        private static ItemInfo? SettingToItem(IReadOnlyDictionary<string, object> setting, string fallBackId)
        {
            if (setting == null
                || !setting.TryGetValue("Provider", out var provider)
                || (!setting.TryGetValue("Id", out var id) && fallBackId == null)
                || !Site.TryParse(provider.ToString(), out var site))
            {
                return null;
            }

            var linkStr = setting.TryGetValue("Link", out var temp) ? temp?.ToString() : null;
            var posterStr = setting.TryGetValue("Poster", out temp) ? temp?.ToString() : null;

            var isSerial = setting.TryGetValue("IsSerial", out var isSerialTemp)
                && (bool)isSerialTemp;

            var isCartoon = setting.TryGetValue("IsCartoon", out var isCartoonTemp)
                && (bool)isCartoonTemp;

            var section = setting.TryGetValue("Section", out var tempSection)
                ? new Section(tempSection.ToString())
                : Section.CreateDefault(
                    (isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
                    | (isCartoon ? SectionModifiers.Cartoon : SectionModifiers.None));

            Uri.TryCreate(linkStr, UriKind.RelativeOrAbsolute, out var link);

            WebImage poster = link == null
                ? posterStr?.ToUriOrNull()
                : posterStr?.ToUriOrNull(link);

            var item = new ItemInfo(site, id?.ToString() ?? fallBackId)
            {
                Link = link,
                Section = section,
                Title = setting.ContainsKey("Title") ? setting["Title"]?.ToString() : null,
                Poster = poster
            };

            return item;
        }

        public static string GenerateFavItemId(ItemInfo item)
        {
            // TODO: it should be item.Site.Value (breaking change)
            return $"fav_s{item.Site}_{item.SiteId}";
        }

        private static FlagsFavoriteTypes GetFlagTypeFromListKind(FavoriteListKind favoriteListKind)
        {
            return favoriteListKind switch
            {
                FavoriteListKind.Favorites => FlagsFavoriteTypes.Favorites,
                FavoriteListKind.ForLater => FlagsFavoriteTypes.ForLater,
                FavoriteListKind.InProcess => FlagsFavoriteTypes.InProcess,
                FavoriteListKind.Finished => FlagsFavoriteTypes.Finished,
                _ => FlagsFavoriteTypes.None,
            };
        }

        [Flags]
        private enum FlagsFavoriteTypes
        {
            None = 0,
            Favorites = 1,
            ForLater = 2,
            InProcess = 4,
            Finished = 8
        }
    }
}

namespace FSClient.Shared.Test.Managers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class BackupManagerTest
    {
        private readonly IEnumerable<(string container, SettingStrategy strategy, string key, object? value)> settingsPairs;

        private readonly IEnumerable<FavoriteListKind> testFavoriteKinds;
        private readonly IEnumerable<(FavoriteListKind kind, ItemInfo item)> favoritesPairs;

        public BackupManagerTest()
        {
            settingsPairs = new[]
            {
                (Settings.UserSettingsContainer, SettingStrategy.Local, "dubl-key", (object?)null),
                (Settings.UserSettingsContainer, SettingStrategy.Local, "local-user-key", 1),
                (Settings.StateSettingsContainer, SettingStrategy.Local, "dubl-key", ""),
                (Settings.StateSettingsContainer, SettingStrategy.Local, "", ""),
                (Settings.UserSettingsContainer, SettingStrategy.Roaming, "roaming", true),
                (Settings.StateSettingsContainer, SettingStrategy.Roaming, "dubl-key", "another")
            };
            testFavoriteKinds = new[] { FavoriteListKind.Favorites, FavoriteListKind.ForLater, FavoriteListKind.InProcess, FavoriteListKind.Finished };

            favoritesPairs = new[]
            {
                (FavoriteListKind.Favorites, new ItemInfo(Site.GetOrCreate("Site1", "Site 1"), "site1_fav1") { Title = "First item name", AddTime = DateTimeOffset.Now }),
                (FavoriteListKind.ForLater, new ItemInfo(Site.GetOrCreate("Site1", "Site 1"), "site1_fav1") { Title = "First item name", AddTime = DateTimeOffset.Now }),
                (FavoriteListKind.Finished, new ItemInfo(Site.GetOrCreate("Site2", "Site 2"), "site2_fav3") { Title = "Second item name", AddTime = DateTimeOffset.Now }),
                (FavoriteListKind.None, new ItemInfo(Site.GetOrCreate("Site3", "Site 3"), "site3_fav3") { Title = "Should not backup", AddTime = DateTimeOffset.Now })
            };
        }

        #region Settings
        [TestCase(BackupDataTypes.StateSettings)]
        [TestCase(BackupDataTypes.UserSettings)]
        [TestCase(BackupDataTypes.UserSettings | BackupDataTypes.StateSettings)]
        public async Task Should_Backup_Settings(BackupDataTypes settingsDataType)
        {
            var expectedList = FilterExpectedSettingsFromBackupType(settingsDataType);
            var settingsMock = new Mock<ISettingService>();
            settingsMock.Setup(s => s.GetAllRawSettings(It.IsAny<string>(), It.IsAny<SettingStrategy>()))
                .Returns(new Dictionary<string, object>());
            foreach (var group in settingsPairs.GroupBy(t => (t.container, t.strategy)))
            {
                settingsMock.Setup(s => s.GetAllRawSettings(group.Key.container, group.Key.strategy))
                    .Returns(group.ToDictionary(k => k.key, v => v.value)!);
            }

            var backupManager = new BackupManager(null!, null!, settingsMock.Object);

            //

            var backupDate = await backupManager
                .BackupAsync(
                    settingsDataType,
                    CancellationToken.None)
                .ConfigureAwait(false);

            //

            foreach (var (container, strategy, key, value) in expectedList)
            {
                var settingValue = backupDate.Settings[strategy][container][key];
                Assert.That(settingValue, Is.EqualTo(value), "Backuped setting value does not match");
            }
        }

        [TestCase(BackupDataTypes.StateSettings)]
        [TestCase(BackupDataTypes.UserSettings)]
        [TestCase(BackupDataTypes.UserSettings | BackupDataTypes.StateSettings)]
        public async Task Should_Load_From_Backup_Settings(BackupDataTypes settingsDataType)
        {
            var expectedList = FilterExpectedSettingsFromBackupType(settingsDataType);
            var backupedList = new List<(string, SettingStrategy, string, object)>();
            var settingsMock = new Mock<ISettingService>();
            settingsMock.Setup(s => s.SetRawSettings(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<SettingStrategy>()))
                .Returns<string, IReadOnlyDictionary<string, object>, SettingStrategy>((_, d, __) => d.Count)
                .Callback<string, IReadOnlyDictionary<string, object>, SettingStrategy>((c, d, s) => backupedList
                    .AddRange(d.Select(pair => (c, s, pair.Key, pair.Value))));

            var backupManager = new BackupManager(null!, null!, settingsMock.Object);

            var backupData = new BackupData
            {
                Settings = settingsPairs
                    .GroupBy(t => t.strategy)
                    .ToDictionary(sg => sg.Key, sg => (IDictionary<string, IDictionary<string, object>>)sg.GroupBy(t => t.container)
                        .ToDictionary(cg => cg.Key, cg => (IDictionary<string, object>)cg
                            .ToDictionary(t => t.key, t => t.value)))
            };

            //

            var result = await backupManager
                .RestoreFromBackupAsync(
                    backupData,
                    settingsDataType,
                    CancellationToken.None)
                .ConfigureAwait(false);

            //

            Assert.That(result.SettingsCount, Is.EqualTo(expectedList.Count), "SettingsCount");
            Assert.That(result.SettingsRestoredCount, Is.EqualTo(backupedList.Count), "SettingsRestoredCount");

            CollectionAssert.AreEquivalent(expectedList, backupedList, "Improted data doesn't match original");
        }
        #endregion

        #region Favorites
        [Test]
        public async Task Should_Backup_Favorites()
        {
            var favoritesMock = new Mock<IFavoriteManager>();
            favoritesMock.SetupGet(s => s.AvailableListKinds)
                .Returns(testFavoriteKinds.ToArray());
            foreach (var kind in testFavoriteKinds)
            {
                favoritesMock.Setup(s => s.GetFavorites(It.Is<FavoriteListKind>(t => t == kind)))
                    .Returns(favoritesPairs.Where(t => t.kind == kind).Select(t => new FavoriteItem(t.item, t.kind)).ToAsyncEnumerable());
            }

            var backupManager = new BackupManager(null!, favoritesMock.Object, null!);

            //

            var backupDate = await backupManager
                .BackupAsync(
                    BackupDataTypes.Favorites,
                    CancellationToken.None)
                .ConfigureAwait(false);

            //

            foreach (var (kind, item) in favoritesPairs.Where(tuple => testFavoriteKinds.Contains(tuple.kind)))
            {
                var backupedItem = backupDate.Items.FirstOrDefault(i => i.Id == item.SiteId && i.Site == item.Site && i.Title == item.Title);
                Assert.That(backupedItem, Is.Not.Null, "Some item is not backuped");

                var backupedFav = backupDate.Favorites.FirstOrDefault(f => f.Id == item.SiteId && f.Site == item.Site && f.KindNameId == kind.ToString());
                Assert.That(backupedFav, Is.Not.Null, "Some fav is not backuped");
            }
            foreach (var (kind, item) in favoritesPairs.Where(tuple => !testFavoriteKinds.Contains(tuple.kind)))
            {
                var backupedFav = backupDate.Favorites.FirstOrDefault(f => f.Id == item.SiteId && f.Site == item.Site && f.KindNameId == kind.ToString());
                Assert.That(backupedFav, Is.Null, "Some fav is backuped, but shouldn't");
            }
        }

        [Test]
        public async Task Should_Load_From_Backup_Favorites()
        {
            var savedFavs = new List<(FavoriteListKind kind, ItemInfo item)>();
            var favoritesMock = new Mock<IFavoriteManager>();
            favoritesMock.Setup(s => s.AddToListAsync(It.IsAny<ItemInfo>(), It.IsAny<FavoriteListKind>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<bool>(true))
                .Callback<ItemInfo, FavoriteListKind, CancellationToken>((item, kind, _) => savedFavs.Add((kind, item)));
            favoritesMock.Setup(s => s.IsSupportedByProvider(It.IsAny<ItemInfo>()))
                .Returns(true);
            favoritesMock.SetupGet(s => s.AvailableListKinds)
                .Returns(testFavoriteKinds.ToArray());

            var backupManager = new BackupManager(null!, favoritesMock.Object, null!);

            var backupData = new BackupData
            {
                Items = favoritesPairs.Select(f => f.item)
                    .GroupBy(i => (i.Site, i.SiteId))
                    .Select(g => g.First())
                    .Select(i => new ItemBackupData
                    {
                        AddDateTime = i.AddTime,
                        Id = i.SiteId,
                        Site = i.Site,
                        Link = i.Link,
                        Poster = i.Poster,
                        Section = i.Section,
                        Title = i.Title
                    })
                    .ToArray(),
                Favorites = favoritesPairs
                    .Select(tuple => new FavoriteBackupData
                    {
                        Id = tuple.item.SiteId,
                        Site = tuple.item.Site,
                        KindNameId = tuple.kind.ToString()
                    })
                    .ToArray()
            };

            //

            var result = await backupManager
                .RestoreFromBackupAsync(
                    backupData,
                    BackupDataTypes.Favorites,
                    CancellationToken.None)
                .ConfigureAwait(false);

            //

            Assert.That(result.FavoritesCount, Is.EqualTo(favoritesPairs.Count()));
            Assert.That(result.FavoritesRestoredCount, Is.EqualTo(savedFavs.Count));

            foreach (var (kind, item) in favoritesPairs.Where(tuple => testFavoriteKinds.Contains(tuple.kind)))
            {
                var backupedItem = savedFavs
                    .FirstOrDefault(tuple => tuple.kind == kind && tuple.item == item);
                Assert.That(backupedItem.item, Is.Not.Null, "Some item is not backuped");
                Assert.That(backupedItem.item.Title, Is.EqualTo(item.Title));
                Assert.That(backupedItem.item.Section, Is.EqualTo(item.Section));
                Assert.That(backupedItem.item.Link, Is.EqualTo(item.Link));
                Assert.That(backupedItem.item.Poster, Is.EqualTo(item.Poster));
            }
            foreach (var (kind, item) in favoritesPairs.Where(tuple => !testFavoriteKinds.Contains(tuple.kind)))
            {
                var notBackupedItem = savedFavs
                    .FirstOrDefault(tuple => tuple.kind == kind && tuple.item == item);
                Assert.That(notBackupedItem.item, Is.Null, "Some item is backuped, but shouldn't");
            }
        }

        #endregion

        #region Json

        [TestCase(1, 10, 12, 15, BackupDataTypes.All, "./V1_Fav_Hist_IntSet_UsrSet.json")]
        public async Task Should_Restore_From_Json(int version,
            int histCount, int favsCount, int settCount,
            BackupDataTypes dataTypes, string fileName)
        {
            var historyMock = new Mock<IHistoryManager>();
            historyMock.Setup(s => s.UpsertAsync(It.IsAny<IEnumerable<HistoryItem>>()))
                .Returns<IEnumerable<HistoryItem>>(input => Task.FromResult(input.Count()));
            var favoritesMock = new Mock<IFavoriteManager>();
            favoritesMock.Setup(s => s.AddToListAsync(It.IsAny<ItemInfo>(), It.IsAny<FavoriteListKind>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<bool>(true));
            favoritesMock.Setup(s => s.IsSupportedByProvider(It.IsAny<ItemInfo>()))
                .Returns(true);
            favoritesMock.SetupGet(s => s.AvailableListKinds)
                .Returns(testFavoriteKinds.ToArray());
            var settingsMock = new Mock<ISettingService>();
            settingsMock.Setup(s => s.SetRawSettings(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<SettingStrategy>()))
                .Returns<string, IReadOnlyDictionary<string, object>, SettingStrategy>((_, d, __) => d.Count);

            var backupManager = new BackupManager(historyMock.Object, favoritesMock.Object, settingsMock.Object);

            var data = await TestFilesHelper.ReadAsJsonAsync<BackupData>(Path.Combine("./Backup", fileName)).ConfigureAwait(false);
            if (data == null)
            {
                throw new InvalidOperationException("Test file is missed.");
            }

            var result = await backupManager.RestoreFromBackupAsync(data, dataTypes, default).ConfigureAwait(false);

            Assert.That(data.Version, Is.EqualTo(version), "Version");
            Assert.That(result.HistoryCount, Is.EqualTo(histCount), "HistoryCount");
            Assert.That(result.HistoryRestoredCount, Is.EqualTo(histCount), "HistoryRestoredCount");
            Assert.That(result.FavoritesCount, Is.EqualTo(favsCount), "FavoritesCount");
            Assert.That(result.FavoritesRestoredCount, Is.EqualTo(favsCount), "FavoritesRestoredCount");
            Assert.That(result.SettingsCount, Is.EqualTo(settCount), "SettingsCount");
            Assert.That(result.SettingsRestoredCount, Is.EqualTo(settCount), "SettingsRestoredCount");
        }

        #endregion

        #region Helpers
        private List<(string, SettingStrategy, string, object?)> FilterExpectedSettingsFromBackupType(BackupDataTypes settingsDataType)
        {
            return settingsDataType.HasFlag(BackupDataTypes.UserSettings | BackupDataTypes.StateSettings)
                ? settingsPairs.ToList()
                : settingsDataType.HasFlag(BackupDataTypes.UserSettings)
                ? settingsPairs.Where(t => t.container == Settings.UserSettingsContainer).ToList()
                : settingsDataType.HasFlag(BackupDataTypes.StateSettings)
                ? settingsPairs.Where(t => t.container == Settings.StateSettingsContainer).ToList()
                : throw new InvalidOperationException("Invalid test input");
        }
        #endregion
    }
}

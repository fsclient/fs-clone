namespace FSClient.Shared.Models
{
    using System.Collections.Generic;

    using FSClient.Shared.Services;

    /// <summary>
    /// Backup content
    /// </summary>
    public class BackupData
    {
        /// <summary>
        /// Backup compatibility version
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Backuped items used in <see cref="Favorites"/> and <see cref="History"/>
        /// </summary>
        public ICollection<ItemBackupData> Items { get; set; }
            = new List<ItemBackupData>();

        /// <summary>
        /// Backuped favorite items from <see cref="Items"/>
        /// </summary>
        public ICollection<FavoriteBackupData> Favorites { get; set; }
            = new List<FavoriteBackupData>();

        /// <summary>
        /// Backuped history items from <see cref="Items"/>
        /// </summary>
        public ICollection<HistoryBackupData> History { get; set; }
            = new List<HistoryBackupData>();

        /// <summary>
        /// Key-value settings per container per <see cref="SettingStrategy"/>
        /// </summary>
        public IDictionary<SettingStrategy, IDictionary<string, IDictionary<string, object>>> Settings { get; set; }
            = new Dictionary<SettingStrategy, IDictionary<string, IDictionary<string, object>>>();
    }
}

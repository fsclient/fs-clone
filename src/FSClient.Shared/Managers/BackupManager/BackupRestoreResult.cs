namespace FSClient.Shared.Models
{
    /// <summary>
    /// Backup restore results
    /// </summary>
    public class BackupRestoreResult
    {
        /// <summary>
        /// Count of favorites in backup data
        /// </summary>
        public int FavoritesCount { get; set; }

        /// <summary>
        /// Count of restored favorites
        /// </summary>
        public int FavoritesRestoredCount { get; set; }

        /// <summary>
        /// Count of history in backup data
        /// </summary>
        public int HistoryCount { get; set; }

        /// <summary>
        /// Count of restored history
        /// </summary>
        public int HistoryRestoredCount { get; set; }

        /// <summary>
        /// Count of settings in backup data
        /// </summary>
        public int SettingsCount { get; set; }

        /// <summary>
        /// Count of restored settings
        /// </summary>
        public int SettingsRestoredCount { get; set; }
    }
}

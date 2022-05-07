namespace FSClient.Shared.Managers
{
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    /// <summary>
    /// Backup creation and restoring manager.
    /// </summary>
    public interface IBackupManager
    {
        /// <summary>
        /// Backup compatibility version.
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Backups application data.
        /// </summary>
        /// <param name="backupDataType">Data to backup.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Backup content.</returns>
        Task<BackupData> BackupAsync(
            BackupDataTypes backupDataType, CancellationToken cancellationToken);

        /// <summary>
        /// Restore backup to application.
        /// </summary>
        /// <param name="backupData">Backup content.</param>
        /// <param name="backupDataType">Data to restore from backup.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Backup restore result.</returns>
        Task<BackupRestoreResult> RestoreFromBackupAsync(
            BackupData backupData, BackupDataTypes backupDataType, CancellationToken cancellationToken);

        /// <summary>
        /// Calculate possible backup types from <see cref="BackupData"/>.
        /// </summary>
        /// <param name="backupData">Backup content.</param>
        /// <returns>Possible backup types.</returns>
        BackupDataTypes GetPossibleTypes(BackupData backupData);
    }
}

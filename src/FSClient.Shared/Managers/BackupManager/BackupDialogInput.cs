namespace FSClient.Shared.Models
{
    /// <summary>
    /// Input arguments for <see cref="Services.Interfaces.IContentDialog{BackupDialogInput, BackupDialogOutput}"/>
    /// </summary>
    public class BackupDialogInput
    {
        public BackupDialogInput(string caption, BackupDataTypes allowedTypes = BackupDataTypes.All)
        {
            (Caption, AllowedTypes) = (caption, allowedTypes);
        }

        /// <summary>
        /// Dialog caption
        /// </summary>
        public string Caption { get; }

        /// <summary>
        /// Possible types to backup or restore from backup
        /// </summary>
        public BackupDataTypes AllowedTypes { get; }
    }
}

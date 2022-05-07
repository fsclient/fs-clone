namespace FSClient.Shared.Models
{
    /// <summary>
    /// Output results for <see cref="Services.Interfaces.IContentDialog{BackupDialogInput, BackupDialogOutput}"/>
    /// </summary>
    public class BackupDialogOutput
    {
        public BackupDialogOutput(BackupDataTypes selectedTypes)
        {
            SelectedTypes = selectedTypes;
        }

        /// <summary>
        /// Selected data types to backup or restore from backup
        /// </summary>
        public BackupDataTypes SelectedTypes { get; }
    }
}

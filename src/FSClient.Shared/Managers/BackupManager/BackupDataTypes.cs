namespace FSClient.Shared.Models
{
    using System;

    /// <summary>
    /// Flags to import/export specific backup data
    /// </summary>
    [Flags]
    public enum BackupDataTypes
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,

        /// <summary>
        /// User seeings
        /// </summary>
        UserSettings = 1,

        /// <summary>
        /// Application state settings
        /// </summary>
        StateSettings = 1 << 1,

        /// <summary>
        /// Internal application settings
        /// </summary>
        InternalSettings = 1 << 2,

        /// <summary>
        /// User history
        /// </summary>
        History = 1 << 3,

        /// <summary>
        /// User favorites
        /// </summary>
        Favorites = 1 << 4,

        /// <summary>
        /// All
        /// </summary>
        All = ~None
    }
}

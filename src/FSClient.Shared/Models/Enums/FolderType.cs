namespace FSClient.Shared.Models
{
    public enum FolderType
    {
        /// <summary>
        /// Unknown folder type.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Specific provider root folder.
        /// Always skipped, only children is displayed.
        /// </summary>
        ProviderRoot,

        /// <summary>
        /// Specific translate folder.
        /// Can be skipped, if single translate folder.
        /// </summary>
        Translate,

        /// <summary>
        /// Specific season folder.
        /// Cannot be skipped.
        /// </summary>
        Season,

        /// <summary>
        /// Specific season folder.
        /// Can be skipped, if single item folder.
        /// </summary>
        Item,

        /// <summary>
        /// Specific episode folder.
        /// Usually contains episode translates files from non-standard providers.
        /// Cannot be skipped.
        /// </summary>
        Episode
    }
}

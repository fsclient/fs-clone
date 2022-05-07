namespace FSClient.Shared.Providers
{
    using FSClient.Shared.Models;

    /// <summary>
    /// Search result item display mode.
    /// </summary>
    public enum DisplayItemMode
    {
        /// <summary>
        /// Guarantee availability of <see cref="ItemInfo.Poster"/> and <see cref="ItemInfo.Title"/>.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// Guarantee availability of <see cref="ItemInfo.Title"/>.
        /// </summary>
        Minimal,

        /// <summary>
        /// Guarantee availability of <see cref="ItemInfo.Poster"/>, <see cref="ItemInfo.Title"/>, <see cref="ItemInfo.Details.Description"/>.
        /// </summary>
        Detailed
    }
}

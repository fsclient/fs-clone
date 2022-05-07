namespace FSClient.Shared.Models
{
    /// <summary>
    /// Video file track
    /// </summary>
    public interface ITrack
    {
        /// <summary>
        /// Track language.
        /// NULL for unknown language.
        /// </summary>
        public string? Language { get; }

        /// <summary>
        /// Track index
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Track title
        /// </summary>
        public string? Title { get; set; }
    }
}

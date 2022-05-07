namespace FSClient.Shared.Models
{
    /// <summary>
    /// Folder position behaviour
    /// </summary>
    public enum PositionBehavior
    {
        /// <summary>
        /// Folder position can't be calculated
        /// </summary>
        None,

        /// <summary>
        /// Folder position equals to max child position
        /// </summary>
        Max,

        /// <summary>
        /// Folder position equals to average children position
        /// </summary>
        Average
    }
}

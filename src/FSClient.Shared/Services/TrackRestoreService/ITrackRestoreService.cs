namespace FSClient.Shared.Services
{
    using FSClient.Shared.Models;

    /// <summary>
    /// Service to save and restore chosen by user track
    /// </summary>
    public interface ITrackRestoreService
    {
        /// <summary>
        /// Save chosen by user track
        /// If track = null, then delete previous value
        /// </summary>
        /// <typeparam name="TTrack">Instance of <see cref="ITrack"/></typeparam>
        /// <param name="track">Chosen track</param>
        /// <param name="file">File owned track</param>
        void Save<TTrack>(TTrack? track, File? file)
            where TTrack : class, ITrack;

        /// <summary>
        /// Restore chosen by user track
        /// </summary>
        /// <typeparam name="TTrack">Instance of <see cref="ITrack"/></typeparam>
        /// <param name="tracks">Available tracks</param>
        /// <param name="file">File owned track</param>
        /// <returns>Restored track</returns>
        TTrack? Restore<TTrack>(TrackCollection<TTrack> tracks, File? file)
            where TTrack : class, ITrack;
    }
}

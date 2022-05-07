namespace FSClient.Shared.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    /// <summary>
    /// Third-party player provider
    /// </summary>
    public interface IThirdPartyPlayer
    {
        /// <summary>
        /// Player detailed info
        /// </summary>
        ThirdPartyPlayerDetails Details { get; }

        /// <summary>
        /// Checks is player available on current device
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if player is available</returns>
        ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Open video in player
        /// </summary>
        /// <param name="video">Video to open</param>
        /// <param name="subtitleTrack">Subtitle track to open</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Enum-based result</returns>
        ValueTask<ThirdPartyPlayerOpenResult> OpenVideoAsync(Video video, SubtitleTrack? subtitleTrack, CancellationToken cancellationToken);
    }
}

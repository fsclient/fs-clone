namespace FSClient.Shared.Models
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPreloadableNode : ITreeNode
    {
        /// <summary>
        /// Is node preloaded
        /// </summary>
        bool IsPreloaded { get; }

        /// <summary>
        /// Is node loading
        /// </summary>
        bool IsLoading { get; }

        /// <summary>
        /// Is node loading faiiled
        /// </summary>
        bool IsFailed { get; }

        /// <summary>
        /// Preload node from lazy factory
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Is successfully preloaded</returns>
        ValueTask<bool> PreloadAsync(CancellationToken cancellationToken);
    }
}

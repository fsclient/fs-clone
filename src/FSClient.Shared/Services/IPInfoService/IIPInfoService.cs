namespace FSClient.Shared.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// IP information service
    /// </summary>
    public interface IIPInfoService
    {
        /// <summary>
        /// Gets current user IP information from third-party service
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current user IP information</returns>
        Task<IPInfo?> GetCurrentUserIPInfoAsync(
            CancellationToken cancellationToken);
    }
}

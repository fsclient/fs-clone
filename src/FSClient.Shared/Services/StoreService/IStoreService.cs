namespace FSClient.Shared.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IStoreService
    {
        DistributionType DistributionType { get; }

        Task<CheckForUpdatesResult> CheckForUpdatesAsync(CancellationToken cancellationToken);
    }
}

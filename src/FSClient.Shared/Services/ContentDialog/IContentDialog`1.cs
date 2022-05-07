namespace FSClient.Shared.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IContentDialog<TOutput>
    {
        Task<TOutput> ShowAsync(CancellationToken cancellationToken);
    }
}

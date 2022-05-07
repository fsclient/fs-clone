namespace FSClient.Shared.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IContentDialog<TInput, TOutput>
    {
        Task<TOutput> ShowAsync(TInput arg, CancellationToken cancellationToken);
    }
}

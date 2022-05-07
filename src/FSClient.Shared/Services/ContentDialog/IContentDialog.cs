namespace FSClient.Shared.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IContentDialog
    {
        Task ShowAsync(CancellationToken cancellationToken);
    }
}

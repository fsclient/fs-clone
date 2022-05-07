namespace FSClient.Shared.Mvvm
{
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    public interface IAsyncCommand : ICommand
    {
        Task ExecuteAsync(object? parameter, CancellationToken cancellationToken = default);
    }
}

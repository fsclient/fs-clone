namespace FSClient.Shared.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface INotificationService
    {
        Task ShowAsync(string line, NotificationType type);

        Task ShowClosableAsync(string line, NotificationType type, CancellationToken cancellationToken);
    }
}

namespace FSClient.UWP.Shared.Activation
{
    using System.Threading.Tasks;

    public interface IActivationHandler
    {
        Task HandleAsync(object args);
        bool CanHandle(object args);
    }
}

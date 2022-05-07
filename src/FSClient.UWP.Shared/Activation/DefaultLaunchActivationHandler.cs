namespace FSClient.UWP.Shared.Activation
{
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Activation;

    using FSClient.Shared;
    using FSClient.UWP.Shared.Services;

    public class DefaultLaunchActivationHandler : IActivationHandler
    {
        private readonly IWindowsNavigationService navigationService;

        public DefaultLaunchActivationHandler(
            IWindowsNavigationService navigationService)
        {
            this.navigationService = navigationService;
        }

        public Task HandleAsync(object args)
        {
            navigationService.Navigate(Settings.Instance.StartPage);

            return Task.CompletedTask;
        }

        public bool CanHandle(object args)
        {
            return args is IActivatedEventArgs && !navigationService.HasAnyPage;
        }
    }
}

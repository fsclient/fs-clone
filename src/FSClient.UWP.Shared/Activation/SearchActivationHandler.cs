namespace FSClient.UWP.Shared.Activation
{
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Activation;

    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Views.Pages;
    using FSClient.UWP.Shared.Views.Pages.Parameters;

    public class SearchActivationHandler : IActivationHandler
    {
        private readonly IWindowsNavigationService navigationService;

        public SearchActivationHandler(
            IWindowsNavigationService navigationService)
        {
            this.navigationService = navigationService;
        }

        public Task HandleAsync(object args)
        {
            var shareArgs = (SearchActivatedEventArgs)args;
            navigationService.Navigate<SearchPage>(new SearchPageParameter(shareArgs.QueryText));

            return Task.CompletedTask;
        }

        public bool CanHandle(object args)
        {
            return args is SearchActivatedEventArgs searchArgs && searchArgs.Kind == ActivationKind.Search;
        }
    }
}

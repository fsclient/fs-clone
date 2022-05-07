namespace FSClient.UWP.Shared.Activation
{
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Activation;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;

    public class SuspendAndResumeHandler : IActivationHandler
    {
        private const string SavedStateKey = "SavedState";

        private readonly IWindowsNavigationService navigationService;
        private readonly ISettingService settingService;

        public SuspendAndResumeHandler(
            IWindowsNavigationService navigationService,
            ISettingService settingService)
        {
            this.navigationService = navigationService;
            this.settingService = settingService;
        }

        public ValueTask SaveState()
        {
            if (!navigationService.HasAnyPage)
            {
                return new ValueTask();
            }

            return navigationService.RootFrame.Dispatcher.CheckBeginInvokeOnUI(() =>
            {
                var currentPage = navigationService.RootFrame.Content;
                var stateToSave = ((currentPage as IStateSaveableProvider)?.StateSaveable
                                   ?? currentPage as IStateSaveable)?.SaveStateToUri();

                settingService.SetSetting(Settings.StateSettingsContainer, SavedStateKey, stateToSave?.ToString());
            });
        }

        public Task HandleAsync(object args)
        {
            var uri = settingService
                .GetSetting(Settings.StateSettingsContainer, SavedStateKey, (string?)null)?
                .ToUriOrNull();
            if (uri != null
                && ProtocolHandlerHelper.GetNavigationItemFromUri(uri) is NavigationItem navigationItem
                && navigationItem.Type != null)
            {
                navigationService.Navigate(navigationItem.Type, navigationItem.Parameter);
            }

            return Task.CompletedTask;
        }

        public bool CanHandle(object args)
        {
            return (args as IActivatedEventArgs)?.PreviousExecutionState == ApplicationExecutionState.Terminated;
        }
    }
}

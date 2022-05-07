namespace FSClient.UWP.Startup
{
    using Windows.ApplicationModel;
    using Windows.ApplicationModel.Activation;
    using Windows.UI.Xaml;

    using FSClient.UWP.Shared.Services;

    public sealed partial class App : Application
    {
        private readonly ActivationService activationService;

        public App()
        {
            activationService = new ActivationService();

            InitializeComponent();
            Suspending += OnSuspending;
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (!args.PrelaunchActivated)
            {
                await activationService.ActivateAsync(Window.Current, args);
            }
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await activationService.ActivateAsync(Window.Current, args);
        }

        protected override async void OnShareTargetActivated(ShareTargetActivatedEventArgs args)
        {
            await activationService.ActivateAsync(Window.Current, args);
        }

        protected override async void OnSearchActivated(SearchActivatedEventArgs args)
        {
            await activationService.ActivateAsync(Window.Current, args);
        }

        private async void OnSuspending(object _, SuspendingEventArgs args)
        {
            var deferral = args.SuspendingOperation?.GetDeferral();
            try
            {
                await activationService.DeactivateAsync(appSuspending: true).ConfigureAwait(true);
            }
            finally
            {
                deferral?.Complete();
            }
        }
    }
}

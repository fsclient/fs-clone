namespace FSClient.UWP.Shared.Activation
{
    using System;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Activation;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;

    public class ShareTargetActivationHandler : IActivationHandler
    {
        private readonly ILauncherService launcherService;

        public ShareTargetActivationHandler(
            ILauncherService launcherService)
        {
            this.launcherService = launcherService;
        }

        public async Task HandleAsync(object args)
        {
            var shareArgs = (ShareTargetActivatedEventArgs)args;

            shareArgs.ShareOperation.ReportStarted();
            try
            {
                var link = await shareArgs.ShareOperation.Data.GetWebLinkAsync();
                var builder = new UriBuilder(link);
                builder.Scheme = UriParserHelper.AppProtocol;

                await launcherService.LaunchUriAsync(builder.Uri).ConfigureAwait(true);

                shareArgs.ShareOperation.ReportCompleted();
            }
            catch (Exception ex)
            {
                shareArgs.ShareOperation.ReportError(ex.Message);
                throw;
            }
        }

        public bool CanHandle(object args)
        {
            return args is ShareTargetActivatedEventArgs shareArgs && shareArgs.Kind == ActivationKind.ShareTarget;
        }
    }
}

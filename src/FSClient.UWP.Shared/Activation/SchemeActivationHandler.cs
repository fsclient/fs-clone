namespace FSClient.UWP.Shared.Activation
{
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Activation;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;

    public class SchemeActivationHandler : IActivationHandler
    {
        private readonly IWindowsNavigationService navigationService;
        private readonly IHistoryManager historyManager;

        public SchemeActivationHandler(
            IWindowsNavigationService navigationService,
            IHistoryManager historyManager)
        {
            this.navigationService = navigationService;
            this.historyManager = historyManager;
        }

        public async Task HandleAsync(object args)
        {
            var activationArgs = (IActivatedEventArgs)args;
            var argumentUri = (args as ProtocolActivatedEventArgs)?.Uri
                              ?? (UriParserHelper.AppProtocol + "://?" + (args as LaunchActivatedEventArgs)?.Arguments).ToUriOrNull();

            if (argumentUri == null)
            {
                return;
            }

            if (argumentUri.Host == "action")
            {
                var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
                ci.NumberFormat.CurrencyDecimalSeparator = ".";

                var query = QueryStringHelper.ParseQuery(argumentUri.Query ?? "")
                    .ToDictionary(p => p.Key, p => p.Value);
                if (query.TryGetValue("position", out var filePositionStr)
                    && float.TryParse(filePositionStr, NumberStyles.Any, ci, out var filePosition)
                    && query.TryGetValue("fileId", out var fileId))
                {
                    var historyEntity = await historyManager.GetHistory()
                        .FirstOrDefaultAsync(entity => entity.Node?.Key == fileId)
                        .ConfigureAwait(false);
                    if (historyEntity != null)
                    {
                        if (historyEntity.Node != null)
                        {
                            historyEntity.Node.Position = filePosition;
                        }

                        await historyManager.UpsertAsync(new[] {historyEntity});
                    }
                }
            }
            else if (ProtocolHandlerHelper.GetNavigationItemFromUri(argumentUri) is NavigationItem navigationItem
                     && navigationItem.Type != null)
            {
                navigationService.Navigate(navigationItem.Type, navigationItem.Parameter);
            }

            await Task.CompletedTask;
        }

        public bool CanHandle(object args)
        {
            try
            {
                return ((args as ProtocolActivatedEventArgs)?.Uri?.IsAbsoluteUri ?? false)
                       || !string.IsNullOrEmpty((args as LaunchActivatedEventArgs)?.Arguments);
            }
            catch
            {
                return false;
            }
        }
    }
}

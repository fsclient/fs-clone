#nullable enable
namespace FSClient.UWP.Background.Tasks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Background;

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Services;

    using Microsoft.Extensions.Logging;

    public sealed class MirrorUpdaterTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();

            var cts = new CancellationTokenSource();
            taskInstance.Canceled += (sender, reason) => cts?.Cancel();
            ViewModelLocator? viewModelLocator = null;

            try
            {
                viewModelLocator = new ViewModelLocator(isReadOnly: true);
                UWPLoggerHelper.InitGlobalHandlers();

                await UpdateMirrorsAsync(viewModelLocator, cts.Token);
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            when (Logger.Initialized)
            {
                Logger.Instance.LogError(ex);
            }
            finally
            {
                cts.Dispose();
                viewModelLocator?.Dispose();
                deferral.Complete();
            }
        }

        private Task UpdateMirrorsAsync(ViewModelLocator viewModelLocator, CancellationToken cancellationToken)
        {
            var applicationService = viewModelLocator.Resolve<IApplicationService>();
            return applicationService.LoadApplicationGlobalSettingsToCacheAsync(cancellationToken);
        }
    }
}

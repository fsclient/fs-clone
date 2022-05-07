namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Views.Controls;

    public class InAppNotificationService : INotificationService
    {
        private static readonly ThreadLocal<InAppNotificationControl?> cachePerUiThread =
            new ThreadLocal<InAppNotificationControl?>();

        private static bool showingNotificationLog;

        public Task ShowAsync(string line, NotificationType type)
        {
            return ShowClosableAsync(line, type, default);
        }

        public async Task ShowClosableAsync(string line, NotificationType type, CancellationToken cancellationToken)
        {
            if (showingNotificationLog)
            {
                return;
            }

            try
            {
                var control = await GetNotificationControlFromMain().ConfigureAwait(true);

                if (control != null)
                {
                    await control.ShowAsync(line, type, cancellationToken);
                }
            }
            catch (Exception ex) when (Logger.Initialized)
            {
                ex.Data[$"{nameof(InAppNotificationService)}.{nameof(line)}"] = line;
                showingNotificationLog = true;
                Logger.Instance.LogWarning(ex);
            }
            finally
            {
                showingNotificationLog = false;
            }
        }

        private static Task<InAppNotificationControl?> GetNotificationControlFromMain()
        {
            return DispatcherHelper.GetForCurrentOrMainView()
                .CheckBeginInvokeOnUI(async () =>
                {
                    if (cachePerUiThread.Value is { } cached)
                    {
                        return cached;
                    }

                    if (Window.Current?.Content is FrameworkElement rootElement)
                    {
                        await rootElement.WaitForLoadedAsync();
                    }

                    return cachePerUiThread.Value = Window.Current?
                        .Content?
                        .FindVisualChildren<InAppNotificationControl>()
                        .FirstOrDefault();
                });
        }
    }
}

namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Diagnostics;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Core;
#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;


    public static class UWPLoggerHelper
    {
        public static void InitGlobalHandlers()
        {
            CoreApplication.UnhandledErrorDetected -= CoreApplication_UnhandledErrorDetected;
            CoreApplication.UnhandledErrorDetected += CoreApplication_UnhandledErrorDetected;
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
#if DEBUG
            try
            {
                Application.Current.DebugSettings.IsBindingTracingEnabled = true;
                Application.Current.DebugSettings.BindingFailed -= DebugSettings_BindingFailed;
                Application.Current.DebugSettings.BindingFailed += DebugSettings_BindingFailed;
            }
            catch
            {
                Debug.WriteLine("[Warning] Application.Current is not available");
            }
#endif

            AppCenterLogger.StartIfAllowed();
        }

        private static bool InvokeLoggerHandler(Exception? ex, string msg)
        {
            if (!Logger.Initialized)
            {
                return false;
            }

            if (ex != null)
            {
                Logger.Instance.LogError(0, ex, msg);
            }
            else
            {
                Logger.Instance.LogWarning(0, msg);
            }

            return true;
        }

        private static void DebugSettings_BindingFailed(object? sender, BindingFailedEventArgs e)
        {
            InvokeLoggerHandler(null, e.Message);
        }

        private static void CoreApplication_UnhandledErrorDetected(object? sender, UnhandledErrorDetectedEventArgs e)
        {
            if (e.UnhandledError.Handled)
            {
                return;
            }

            try
            {
                e.UnhandledError.Propagate();
            }
            catch (Exception ex)
            {
                var logged = false;
                try
                {
                    var cleanEx = ex is AggregateException aggrEx
                        ? (aggrEx.Flatten().InnerException ?? ex)
                        : ex;
                    cleanEx.Data["Reason"] = "Unhandled Error Detected";
                    logged = InvokeLoggerHandler(cleanEx, cleanEx.Message);
                }
                catch
                {
                    Debug.WriteLine(ex);
                    Debugger.Break();
                }

                if (!logged)
                {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            var isLogged = false;
            try
            {
                if (e.Exception == null
                    || e.Observed)
                {
                    return;
                }

                var ex = e.Exception.Flatten().InnerException ?? e.Exception;
                ex.Data["Reason"] = "Unobserved Task Exception";

                isLogged = InvokeLoggerHandler(ex, ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Debugger.Break();
            }

            if (isLogged)
            {
                e.SetObserved();
            }
        }
    }
}

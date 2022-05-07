namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;

    using Windows.Foundation.Metadata;
    using Windows.Storage;

#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;

    using Microsoft.AppCenter;
    using Microsoft.AppCenter.Analytics;
    using Microsoft.AppCenter.Crashes;
    using Microsoft.Extensions.Logging;

    using Nito.Disposables;

    using LogLevel = Microsoft.Extensions.Logging.LogLevel;

    public sealed class AppCenterLogger : ILogger
    {
        private readonly bool IsApplicationAvailable;
        private bool IsOnBackground;

        private static bool Allowed
        {
            get => ObjectHelper.SafeCast(ApplicationData.Current.LocalSettings.Values[nameof(Settings.UseTelemetry)],
                true);
            set => ApplicationData.Current.LocalSettings.Values[nameof(Settings.UseTelemetry)] = value;
        }

        public static void StartIfAllowed()
        {
            try
            {
                if (string.IsNullOrEmpty(Secrets.AppCenterKey)
                    && AppCenter.Configured)
                {
                    return;
                }

                if (!Allowed)
                {
                    return;
                }

                var countryCode = RegionInfo.CurrentRegion.TwoLetterISORegionName;
                AppCenter.SetCountryCode(countryCode);
                AppCenter.Start(Secrets.AppCenterKey, typeof(Analytics), typeof(Crashes));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Debugger.Break();
            }
        }

        public AppCenterLogger()
        {
            if (Settings.Initialized)
            {
                // Setting up "Allowed" property for next application run, when it will be read with updated value.
                Allowed = Settings.Instance.UseTelemetry;
            }

            try
            {
                if (ApiInformation.IsEventPresent(typeof(Application).FullName, nameof(Application.EnteredBackground)))
                {
                    Application.Current.EnteredBackground += (s, e) => IsOnBackground = true;
                    Application.Current.LeavingBackground += (s, e) => IsOnBackground = false;
                }

                IsApplicationAvailable = true;
            }
            catch (Exception)
            {
                Debug.WriteLine("[Warning] Application.Current is not available");
                IsApplicationAvailable = false;
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Information => true,
                LogLevel.Error => true,
                LogLevel.Critical => true,
                _ => false
            } && AppCenter.Configured && (!Settings.Initialized || Settings.Instance.UseTelemetry);
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NoopDisposable.Instance;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception?, string> formatter)
        {
            try
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                var message = formatter(state, exception);

                var props = exception?.GetFullProperties(UWPAppInformation.Instance)
                            ?? new Dictionary<string, string?>();

                if (logLevel != LogLevel.Information
                    || exception != null)
                {
                    props.Add("Severity", logLevel.ToString());
                    props.Add(nameof(IsOnBackground), IsOnBackground.ToString());
                    props.Add(nameof(IsApplicationAvailable), IsApplicationAvailable.ToString());
                }

                if (logLevel != LogLevel.Information
                    && ViewModelLocator.Initialized)
                {
                    try
                    {
                        props.Add("PageStack", ViewModelLocator.Current.NavigationService.PagesHistory.Reverse()
                            .Aggregate(string.Empty, (res, page) => res + Environment.NewLine + page.ToShortString())
                            .Trim());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        Debugger.Break();
                    }
                }

                if (state != null)
                {
                    if (state is ILogState logState)
                    {
                        foreach (var prop in logState.GetLogProperties(false))
                        {
                            props.Add(prop.Key, prop.Value);
                        }
                    }
                    else if (state is IDictionary<string, string> dictionary)
                    {
                        foreach (var prop in dictionary)
                        {
                            props.Add(prop.Key, prop.Value);
                        }
                    }
                    else
                    {
                        props.Add("State", state.ToString());
                    }
                }

                if (exception != null)
                {
                    if (!FilterException(exception, logLevel))
                    {
                        return;
                    }

                    props.Add("Message", message);

                    Crashes.TrackError(exception, props);
                }
                else
                {
                    Analytics.TrackEvent(message, props);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Crashes.TrackError(ex,
                        new Dictionary<string, string>
                        {
                            ["OriginalException"] = exception?.ToString() ?? formatter(state, exception)
                        });
                }
                catch
                {
                    Debug.WriteLine(ex);
                    Debugger.Break();
                }
            }
        }

        private bool FilterException(Exception exception, LogLevel type)
        {
            if (exception.Message.Contains("OpenClipboard"))
            {
                return false;
            }

            switch (unchecked((uint)exception.HResult))
            {
                // The request is invalid because Shutdown() has been called. (Exception from HRESULT: 0xC00D3E85)
                case 0xC00D3E85:
                // The operation cannot be completed because the window is being closed. (Exception from HRESULT: 0x802A0201)
                case 0x802A0201:
                // The paging file is too small for this operation to complete. (Exception from HRESULT: 0x800705AF)
                case 0x800705AF
                    when IsOnBackground && type != LogLevel.Critical:
                    return false;
                default:
                    return true;
            }
        }

        public sealed class Provider : ILoggerProvider
        {
            // Should be initialized as early as possible
            private static readonly AppCenterLogger appCenterLogger = new AppCenterLogger();

            public ILogger CreateLogger(string categoryName)
            {
                return appCenterLogger;
            }

            public void Dispose()
            {
            }
        }
    }
}

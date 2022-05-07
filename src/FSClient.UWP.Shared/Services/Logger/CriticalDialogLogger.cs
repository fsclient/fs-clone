namespace FSClient.UWP.Shared.Services
{
    using System;

    using Windows.UI.Xaml;

    using FSClient.Localization.Resources;
    using FSClient.UWP.Shared.Views.Dialogs;

    using Microsoft.Extensions.Logging;

    using Nito.Disposables;

    using Humanizer;

    public class CriticalDialogLogger : ILogger
    {
        private readonly LazyDialog<ConfirmDialog, string, bool> confirmDialog = new LazyDialog<ConfirmDialog, string, bool>();

        public IDisposable BeginScope<TState>(TState state)
        {
            return NoopDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel == LogLevel.Critical;
        }

        public async void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = Strings.CriticalDialogLogger_ShouldApplicationExit.FormatWith(formatter(state, exception));
            var shouldClose = await confirmDialog.ShowAsync(message, default).ConfigureAwait(true);

            if (shouldClose)
            {
                Application.Current.Exit();
            }
        }

        public sealed class Provider : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName)
            {
                return new CriticalDialogLogger();
            }

            public void Dispose()
            {
            }
        }
    }
}

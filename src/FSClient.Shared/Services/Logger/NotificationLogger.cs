namespace FSClient.Shared.Services
{
    using System;
    using System.Diagnostics;

    using Microsoft.Extensions.Logging;

    using Nito.Disposables;

    public sealed class NotificationLogger : ILogger
    {
        private readonly Lazy<INotificationService> notificationService;

        public NotificationLogger(Lazy<INotificationService> notificationService)
        {
            this.notificationService = notificationService;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Error => true,
                LogLevel.Critical => true,
                LogLevel.Warning when Debugger.IsAttached => true,
                LogLevel.Debug when Debugger.IsAttached => true,
                _ => false
            } && Settings.Instance.InAppLoggerNotifications;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NoopDisposable.Instance;
        }

        public async void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            await notificationService.Value.ShowAsync(
                $"[{logLevel}]: {exception?.ToString() ?? formatter(state, exception)}",
                GetNotificationType(logLevel)).ConfigureAwait(false);
        }

        private static NotificationType GetNotificationType(LogLevel inputType)
        {
            switch (inputType)
            {
                case LogLevel.Debug:
                    return NotificationType.Debug;
                case LogLevel.Error:
                    return NotificationType.Error;
                case LogLevel.Critical:
                    return NotificationType.Fatal;
                case LogLevel.Warning:
                    return NotificationType.Warning;
                case LogLevel.Information:
                case LogLevel.Trace:
                case LogLevel.None:
                    return NotificationType.Information;
                default:
                    throw new NotSupportedException($"GetNotificationType {inputType} not supported");
            }
        }

        public sealed class Provider : ILoggerProvider
        {
            private readonly Lazy<INotificationService> notificationService;

            public Provider(Lazy<INotificationService> notificationService)
            {
                this.notificationService = notificationService;
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new NotificationLogger(notificationService);
            }

            public void Dispose()
            {
            }
        }
    }
}

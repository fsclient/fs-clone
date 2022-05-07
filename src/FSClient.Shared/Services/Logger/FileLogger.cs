namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;
    using Nito.Disposables;

    public sealed class FileLogger : ILogger, IDisposable
    {
        private const int LogsToKeepOnDisk = 10;

        private readonly AsyncLazy<(FileInfo?, List<LogEvent>)> logsFile;
        private readonly SemaphoreSlim loggingSemaphore;
        private readonly Lazy<IAppInformation> appInformation;
        private readonly Lazy<IStorageService> storageService;

        public FileLogger(Lazy<IAppInformation> appInformation, Lazy<IStorageService> storageService)
        {
            logsFile = new AsyncLazy<(FileInfo?, List<LogEvent>)>(() => Task.Run(PrepareFolderAndGetLogFileAsync));
            loggingSemaphore = new SemaphoreSlim(1);
            this.appInformation = appInformation;
            this.storageService = storageService;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return Settings.Instance.FileLogger;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NoopDisposable.Instance;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var message = formatter(state, exception);

                    using (await loggingSemaphore.LockAsync())
                    {
                        var (file, list) = await logsFile;
                        if (file == null)
                        {
                            return;
                        }

                        using var stream = file.OpenWrite();

                        list.Add(new LogEvent(appInformation.Value, logLevel, message, exception, state));

                        await JsonSerializer.SerializeAsync(stream, list).ConfigureAwait(false);

                        await stream.FlushAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    Debugger.Break();
                }
            });
        }

        private async Task<(FileInfo?, List<LogEvent>)> PrepareFolderAndGetLogFileAsync()
        {
            try
            {
                var newLogFileName = $"fs-log-{DateTimeOffset.Now:yyyy-MM-dd-hh-mm}.json";

                var folderName = await GetLogsFolderAsync().ConfigureAwait(false);

                var directory = new DirectoryInfo(folderName);
                directory.Create();

                try
                {
                    // directory.GetFiles fails on UWP when is used with FutureAccessList
                    var files = Directory.GetFiles(folderName, "fs-log-*", SearchOption.TopDirectoryOnly)
                        .Select(f => new FileInfo(f))
                        .ToArray();
                    newLogFileName = FileHelper.GetUniqueFileName(newLogFileName, files.Select(f => f.Name).ToArray());

                    files.Where(f => f.Name.StartsWith("fs-log-", StringComparison.Ordinal))
                        .OrderByDescending(f => f.CreationTime)
                        .Skip(LogsToKeepOnDisk)
                        .ToList()
                        .ForEach(f => f.Delete());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                var newFile = new FileInfo(Path.Combine(directory.FullName, newLogFileName));
                using var stream = newFile.Create();

                var list = new List<LogEvent>
                {
                    new LogEvent(appInformation.Value, LogLevel.Information, "Log opened", state: appInformation.Value)
                };

                await JsonSerializer
                    .SerializeAsync(stream, list)
                    .ConfigureAwait(false);

                await stream.FlushAsync().ConfigureAwait(false);

                return (newFile, list);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return (null, new List<LogEvent>());
            }
        }

        private async Task<string> GetLogsFolderAsync()
        {
            string? folderPath = null;
            if (Settings.Instance.FileLoggerCustomFolder is string customFolderToken
                && !string.IsNullOrEmpty(customFolderToken))
            {
                var folder = await storageService.Value.GetSavedFolderAsync(customFolderToken).ConfigureAwait(false);
                folderPath = folder?.Path;
            }
            if (folderPath == null
                && storageService.Value.LocalFolder is IStorageFolder localFolder)
            {
                folderPath = Path.Combine(localFolder.Path, StorageServiceExtensions.LogsFolderName);
            }
            if (folderPath == null)
            {
                throw new InvalidOperationException("FileLogger can't initialize folder");
            }

            return folderPath;
        }

        public void Dispose()
        {
            loggingSemaphore.Dispose();
        }

        private record LogEvent
        {
            public LogEvent(
                IAppInformation appInformation,
                LogLevel logLevel,
                string message,
                Exception? exception = null,
                object? state = null)
            {
                DateTime = DateTimeOffset.Now;
                LogLevel = logLevel.ToString();
                Message = message;

                if (exception != null)
                {
                    var props = exception.GetFullProperties(appInformation, true).ToDictionary(k => k.Key, v => (object?)v.Value);
                    if (props.TryGetValue(nameof(exception.StackTrace), out var stackTraceObj))
                    {
                        props[nameof(exception.StackTrace)] = stackTraceObj?
                            .ToString()
                            .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(line => line.Trim());
                    }
                    Exception = props;
                }

                State = state is ILogState logState ? logState.GetLogProperties(false) : state;
            }

            public DateTimeOffset DateTime { get; init; }
            public string LogLevel { get; init; }
            public string Message { get; init; }
            public Dictionary<string, object?>? Exception { get; init; }
            public object? State { get; init; }
        }

        public sealed class Provider : ILoggerProvider
        {
            private readonly Lazy<IAppInformation> appInformation;
            private readonly Lazy<IStorageService> storageService;

            public Provider(Lazy<IAppInformation> appInformation, Lazy<IStorageService> storageService)
            {
                this.appInformation = appInformation;
                this.storageService = storageService;
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new FileLogger(appInformation, storageService);
            }

            public void Dispose()
            {
            }
        }
    }
}

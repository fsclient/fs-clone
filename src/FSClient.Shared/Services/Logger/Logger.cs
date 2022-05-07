namespace FSClient.Shared.Services
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    using Microsoft.Extensions.Logging;

    public static class Logger
    {
        private static ILogger? instance;
        public static ILogger Instance
        {
            get => instance ?? throw new InvalidOperationException("Logger Instance was not setted");
            set => instance = value;
        }

        public static bool Initialized => instance != null;

        public static void LogCritical(
            this ILogger logger,
            Exception exception,
            [CallerMemberName] string memberName = null!,
            [CallerFilePath] string filePath = null!,
            [CallerLineNumber] int lineNumber = 0)
        {
            exception.Data["CallerInformation"] = $"{memberName} on {lineNumber} line in file {filePath}";
            logger.LogCritical(default, exception, exception.Message);
        }

        public static void LogError(
            this ILogger logger,
            Exception exception,
            [CallerMemberName] string memberName = null!,
            [CallerFilePath] string filePath = null!,
            [CallerLineNumber] int lineNumber = 0)
        {
            exception.Data["CallerInformation"] = $"{memberName} on {lineNumber} line in file {filePath}";
            logger.LogError(default, exception, exception.Message);
        }

        public static void LogWarning(
            this ILogger logger,
            Exception exception,
            [CallerMemberName] string memberName = null!,
            [CallerFilePath] string filePath = null!,
            [CallerLineNumber] int lineNumber = 0)
        {
            exception.Data["CallerInformation"] = $"{memberName} on {lineNumber} line in file {filePath}";
            logger.LogWarning(default, exception, exception.Message);
        }

        public static void LogDebug(
            this ILogger logger,
            Exception exception,
            [CallerMemberName] string memberName = null!,
            [CallerFilePath] string filePath = null!,
            [CallerLineNumber] int lineNumber = 0)
        {
            exception.Data["CallerInformation"] = $"{memberName} on {lineNumber} line in file {filePath}";
            logger.LogDebug(default, exception, exception.Message);
        }

        public static Dictionary<string, string?> GetFullProperties(this Exception? exception, IAppInformation appInformation, bool appendStackTrace = false)
        {
            var dictionary = exception?.GetProperties() ?? new Dictionary<string, string?>();

            dictionary.Add("ThreadId", Environment.CurrentManagedThreadId.ToString());
            if (Settings.Initialized)
            {
                dictionary.Add("CurrentSite", Settings.Instance.MainSite.Value ?? "\"Null settings\"");
            }

            var (usage, total) = appInformation.MemoryUsage();
            dictionary.Add("MemoryUsage", $"{usage}/{total}");

            if (appendStackTrace && exception?.StackTrace is string stackTrace)
            {
                dictionary.Add("StackTrace", stackTrace);
            }

            return dictionary;
        }

        public static Dictionary<string, string?> GetProperties(this Exception exception)
        {
            Dictionary<string, string?> dictionary;

            try
            {
                var valuesArray = new DictionaryEntry[exception.Data.Count];
                exception.Data.CopyTo(valuesArray, 0);

                dictionary = valuesArray
                    .ToDictionary(
                        kv => (string)kv.Key,
                        kv => kv.Value?.ToString()?.Trim());
            }
            catch (Exception ex)
            {
                dictionary = new Dictionary<string, string?>
                {
                    ["Error"] = "During " + nameof(GetProperties),
                    ["Exception"] = ex.Message.ToString()
                };
            }

            if (exception is AggregateException aggregateEx)
            {
                exception = aggregateEx.Flatten().GetBaseException() ?? exception;
            }
            if (exception is TypeInitializationException typeException)
            {
                dictionary.Add("TypeName", typeException.TypeName);
            }

            if (exception.InnerException is Exception innerException)
            {
                dictionary.Add("InnerException", innerException.GetType() + ": " + innerException.Message);
            }

            dictionary.Add("HResult", "0x" + exception.HResult.ToString("X4"));

            dictionary.Add("ExceptionTypeName", exception.GetType().ToString());

            return dictionary;
        }
    }
}

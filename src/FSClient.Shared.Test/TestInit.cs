namespace FSClient.Shared.Test
{
    using System;

    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using Moq;

    using NUnit.Framework;

    [SetUpFixture]
    public static class Test
    {
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public static ILogger Logger { get; private set; }
        public static ISettingService SettingService { get; private set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        [OneTimeSetUp]
        public static void MyTestInitialize()
        {
            Shared.Services.Logger.Instance = Logger = new TestLogger();
            SettingService = MockNewSettingService().Object;
            Settings.InitializeSettings(SettingService);
        }

        public static Mock<ISettingService> MockNewSettingService()
        {
            var mock = new Mock<ISettingService>();
            mock.SetupAllProperties();

            return mock;
        }

        private class TestLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
            {
                return Nito.Disposables.NoopDisposable.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                TestContext
                   .WriteLine("[{0}] {1} {2}", logLevel, formatter(state, exception), exception?.Data["CallerInformation"]);
            }
        }
    }
}

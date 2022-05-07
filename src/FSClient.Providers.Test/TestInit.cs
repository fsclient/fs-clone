using NUnit.Framework;

[assembly: Parallelizable(ParallelScope.All), LevelOfParallelism(4)]

namespace FSClient.Providers.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ConsoleTables;

    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared;
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

        public static IProviderConfigService ProviderConfigService { get; } = new ProviderServiceStub();

        [OneTimeSetUp]
        public static void MyOneTimeSetUp()
        {
            Shared.Services.Logger.Instance = Logger = new TestLogger();
            SettingService = MockNewSettingService().Object;
            Settings.InitializeSettings(SettingService);
        }

        public static Mock<ISettingService> MockNewSettingService()
        {
            var mock = new Mock<ISettingService>();
            mock.SetupAllProperties();

            var settings = new Dictionary<string, object>();

            mock.Setup(s => s
                .GetAllRawSettings(It.IsAny<string>(), It.IsAny<SettingStrategy>()))
                .Returns(settings);
            mock.Setup(s => s
                .GetSetting(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SettingStrategy>()))
                .Returns<string, string, string, SettingStrategy>((_, key, otherwise, ___) =>
                {
                    if (key.StartsWith("CachedDomain_", StringComparison.Ordinal)
                        && settings.TryGetValue("CachedDomain_" + key.Split('_')[1], out var cachedDomain))
                    {
                        return (string)cachedDomain;
                    }
                    if (key.StartsWith("UserDomain_", StringComparison.Ordinal)
                        && settings.TryGetValue("UserDomain_" + key.Split('_')[1], out var userDomain))
                    {
                        return (string)userDomain;
                    }
                    return otherwise;
                });
            mock.Setup(s => s
                .SetSetting(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SettingStrategy>()))
                .Returns<string, string, string, SettingStrategy>((_, key, value, ___) =>
                {
                    if (key.StartsWith("CachedDomain_", StringComparison.Ordinal)
                        || key.StartsWith("UserDomain_", StringComparison.Ordinal))
                    {
                        if (settings.ContainsKey(key))
                        {
                            settings[key] = value;
                        }
                        else
                        {
                            settings.Add(key, value);
                        }
                    }
                    return true;
                });
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
                if (state is IEnumerable<string[]> rows)
                {
                    TestContext.WriteLine("[{0}] Table:", logLevel);

                    var table = new ConsoleTable(rows.First())
                        .Configure(o => o.OutputTo = TestContext.Out);

                    foreach (var row in rows.Skip(1))
                    {
                        table.AddRow(row);
                    }

                    table.Write(Format.MarkDown);
                }
                else
                {
                    TestContext.WriteLine("[{0}] {1} {2}", logLevel, formatter(state, exception), exception?.Data["CallerInformation"]);
                }
            }
        }
    }
}

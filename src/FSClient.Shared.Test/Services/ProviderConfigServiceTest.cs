namespace FSClient.Shared.Test.Services
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Services;

    using NUnit.Framework;

    [TestFixture]
    public class ProviderConfigServiceTest
    {
        [Test]
        public void Should_Get_Single_Mirror()
        {
            IProviderConfigService providerConfigService = new ProviderConfigService(Test.SettingService, new ApplicationGlobalSettings());

            var config = new MirrorGetterConfig(new[]
            {
                new Uri("https://github.com")
            });

#pragma warning disable CA2012 // Use ValueTasks correctly
            var mirrorTask = providerConfigService.GetMirrorAsync(default, config, default);
            Assert.That(mirrorTask.IsCompletedSuccessfully, Is.True);
#pragma warning restore CA2012 // Use ValueTasks correctly
            var mirror = mirrorTask.Result;

            Assert.That(mirror, Is.Not.Null);
            Assert.That(mirror.IsAbsoluteUri, Is.True);
            Assert.That(mirror, Is.AnyOf(config.Mirrors.ToArray()));
        }

        [Timeout(5000)]
        [Test]
        public async Task Should_Get_Mirror_From_Cache_On_Second_Call()
        {
            IProviderConfigService providerConfigService = new ProviderConfigService(Test.SettingService, new ApplicationGlobalSettings());

            var config = new MirrorGetterConfig(new[]
            {
                new Uri("https://github.com"),
                new Uri("https://github.io"),
            });

            var mirror1 = await providerConfigService.GetMirrorAsync(default, config, default);

            Assert.That(mirror1, Is.Not.Null);
            Assert.That(mirror1.IsAbsoluteUri, Is.True);
            Assert.That(mirror1, Is.AnyOf(config.Mirrors.ToArray()));

#pragma warning disable CA2012 // Use ValueTasks correctly
            var mirrorTask = providerConfigService.GetMirrorAsync(default, config, default);
            Assert.That(mirrorTask.IsCompletedSuccessfully, Is.True);
#pragma warning restore CA2012 // Use ValueTasks correctly
            var mirror2 = mirrorTask.Result;

            Assert.That(mirror2, Is.EqualTo(mirror1));
        }

        [Timeout(5000)]
        [TestCase(ProviderMirrorCheckingStrategy.Parallel)]
        [TestCase(ProviderMirrorCheckingStrategy.Sequence)]
        public async Task Should_Get_Mirror(ProviderMirrorCheckingStrategy strategy)
        {
            IProviderConfigService providerConfigService = new ProviderConfigService(Test.SettingService, new ApplicationGlobalSettings());

            var config = new MirrorGetterConfig(new[]
            {
                new Uri("https://github.com"),
                new Uri("https://github.io"),
            })
            {
                MirrorCheckingStrategy = strategy
            };

            var mirror = await providerConfigService.GetMirrorAsync(default, config, default);

            Assert.That(mirror, Is.Not.Null);
            Assert.That(mirror.IsAbsoluteUri, Is.True);
            Assert.That(mirror, Is.AnyOf(config.Mirrors.ToArray()));
        }

        [Timeout(15000)]
        [TestCase(ProviderMirrorCheckingStrategy.Parallel)]
        [TestCase(ProviderMirrorCheckingStrategy.Sequence)]
        public async Task Should_Cancel_Get_Mirror(ProviderMirrorCheckingStrategy strategy)
        {
            IProviderConfigService providerConfigService = new ProviderConfigService(Test.SettingService, new ApplicationGlobalSettings());

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var config = new MirrorGetterConfig(new[]
            {
                new Uri("https://github.com"),
                new Uri("https://github.io"),
            })
            {
                MirrorCheckingStrategy = strategy,
                Validator = new Func<System.Net.Http.HttpResponseMessage, CancellationToken, ValueTask<bool>>(async (r, ct) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    return r.IsSuccessStatusCode;
                })
            };

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var mirror = await providerConfigService.GetMirrorAsync(default, config, cts.Token);
            stopwatch.Stop();

            Assert.That(cts.IsCancellationRequested, Is.True);
            Assert.That(stopwatch.Elapsed, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(mirror, Is.Not.Null);
            Assert.That(mirror, Is.EqualTo(config.Mirrors.First()));
            Assert.That(mirror.IsAbsoluteUri, Is.True);
        }
    }
}

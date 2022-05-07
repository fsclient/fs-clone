namespace FSClient.Shared.Helpers.TimeSpanSemaphore
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    // based on https://github.com/joelfillmore/JFLibrary/blob/master/JFLibrary/TimeSpanSemaphore.cs

    internal class SingleTimeSpanSemaphore : ITimeSpanSemaphore
    {
        private readonly SemaphoreSlim pool;
        private readonly int maxCount;
        private readonly TimeSpan resetSpan;
        private readonly ConcurrentQueue<DateTime> releaseTimes;

        public SingleTimeSpanSemaphore(int maxCount, TimeSpan resetSpan)
        {
            pool = new SemaphoreSlim(maxCount, maxCount);
            this.maxCount = maxCount;
            this.resetSpan = resetSpan;

            releaseTimes = new ConcurrentQueue<DateTime>(
                Enumerable.Range(0, maxCount).Select(_ => DateTime.MinValue));
        }

        public async ValueTask<IDisposable> LockAsync(CancellationToken cancellationToken)
        {
            var locked = false;
            try
            {
                await pool.WaitAsync(cancellationToken).ConfigureAwait(false);
                locked = true;

                if (!releaseTimes.TryDequeue(out var oldestRelease))
                {
                    oldestRelease = DateTime.MinValue;
                }

                var now = DateTime.UtcNow;
                var windowReset = oldestRelease.Add(resetSpan);
                if (windowReset > now)
                {
                    var sleepMilliseconds = Math.Max(
                        (int)(windowReset.Subtract(now).Ticks / TimeSpan.TicksPerMillisecond),
                        1);

                    await Task.Delay(sleepMilliseconds, cancellationToken).ConfigureAwait(false);
                }
            }
            catch when (locked)
            {
                Release();
                throw;
            }
            return new Nito.Disposables.AnonymousDisposable(Release);

            void Release()
            {
                releaseTimes.Enqueue(DateTime.UtcNow);

                pool.Release();
            }
        }

        public void Dispose()
        {
            pool.Dispose();
        }

        public override string ToString()
        {
            return $"SingleTimeSpanSemaphore: {pool.CurrentCount} of {maxCount} in {resetSpan} period";
        }
    }
}

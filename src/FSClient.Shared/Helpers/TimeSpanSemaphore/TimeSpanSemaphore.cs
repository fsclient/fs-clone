namespace FSClient.Shared.Helpers.TimeSpanSemaphore
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Nito.Disposables;

    public static class TimeSpanSemaphore
    {
        public static ITimeSpanSemaphore Empty => default(EmptyTimeSpanSemaphore);

        public static ITimeSpanSemaphore Create(int maxCount, TimeSpan resetSpan)
        {
            return new SingleTimeSpanSemaphore(maxCount, resetSpan);
        }

        public static ITimeSpanSemaphore Combine(params ITimeSpanSemaphore[] timeSpanSemaphores)
        {
            return new CombinedTimeSpanSemaphore(timeSpanSemaphores);
        }

        private struct EmptyTimeSpanSemaphore : ITimeSpanSemaphore
        {
            public void Dispose()
            {
            }

            public ValueTask<IDisposable> LockAsync(CancellationToken cancellationToken)
            {
                return new ValueTask<IDisposable>(NoopDisposable.Instance);
            }
        }
    }
}

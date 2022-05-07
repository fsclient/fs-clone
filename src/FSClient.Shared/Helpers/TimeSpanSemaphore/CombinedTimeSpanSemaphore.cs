namespace FSClient.Shared.Helpers.TimeSpanSemaphore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal class CombinedTimeSpanSemaphore : ITimeSpanSemaphore
    {
        private readonly ITimeSpanSemaphore[] timeSpanSemaphores;

        public CombinedTimeSpanSemaphore(params ITimeSpanSemaphore[] timeSpanSemaphores)
        {
            this.timeSpanSemaphores = timeSpanSemaphores;
        }

        public void Dispose()
        {
            foreach (var semaphore in timeSpanSemaphores)
            {
                semaphore.Dispose();
            }
        }

        public async ValueTask<IDisposable> LockAsync(CancellationToken cancellationToken)
        {
            var startedSemaphores = new List<IDisposable>(timeSpanSemaphores.Length);
            try
            {
                for (var i = 0; i < timeSpanSemaphores.Length; i++)
                {
                    var started = await timeSpanSemaphores[i].LockAsync(cancellationToken).ConfigureAwait(false);
                    startedSemaphores.Add(started);
                }
            }
            catch
            {
                foreach (var started in startedSemaphores)
                {
                    started.Dispose();
                }
                throw;
            }

            return new Nito.Disposables.AnonymousDisposable(() =>
            {
                foreach (var started in startedSemaphores)
                {
                    started.Dispose();
                }
            });
        }

        public override string ToString()
        {
            return string.Join(Environment.NewLine, timeSpanSemaphores.Select(s => s.ToString()));
        }
    }
}

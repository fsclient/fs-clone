namespace FSClient.Shared.Helpers.TimeSpanSemaphore
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ITimeSpanSemaphore : IDisposable
    {
        ValueTask<IDisposable> LockAsync(CancellationToken cancellationToken);
    }
}

namespace FSClient.Shared.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICacheService
    {
        ValueTask<TType> GetOrAddAsync<TType>(
            string key,
            Func<string, CancellationToken, Task<TType>> addFunc,
            TimeSpan maxAge,
            CancellationToken cancellationToken);
    }
}

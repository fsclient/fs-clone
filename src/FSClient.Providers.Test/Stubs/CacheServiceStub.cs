namespace FSClient.Providers.Test.Stubs
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Services;

    public class CacheServiceStub : ICacheService
    {
        private readonly Func<string, CancellationToken, Task<object>>? addFunc;

        public CacheServiceStub()
        {
        }

        public CacheServiceStub(Func<string, CancellationToken, Task<object>> addFunc)
        {
            this.addFunc = addFunc;
        }

        public async ValueTask<TType> GetOrAddAsync<TType>(
            string key,
            Func<string, CancellationToken, Task<TType>> addFunc,
            TimeSpan maxAge, CancellationToken cancellationToken)
        {
            return this.addFunc != null
                ? (TType)await this.addFunc(key, cancellationToken).ConfigureAwait(false)
                : await addFunc(key, cancellationToken).ConfigureAwait(false);
        }
    }
}

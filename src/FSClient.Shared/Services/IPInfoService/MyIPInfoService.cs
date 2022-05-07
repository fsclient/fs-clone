namespace FSClient.Shared.Services
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers.Web;

    /// <inheritdoc cref="IIPInfoService" />
    public sealed class MyIPInfoService : IIPInfoService, IDisposable
    {
        private readonly HttpClient httpClient;

        public MyIPInfoService()
        {
            httpClient = new HttpClient();
        }

        /// <inheritdoc/>
        public Task<IPInfo?> GetCurrentUserIPInfoAsync(CancellationToken cancellationToken)
        {
            return httpClient
                .GetBuilder(new Uri("https://api.myip.com/"))
                .SendAsync(cancellationToken)
                .AsJson<IPInfo>(cancellationToken);
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}

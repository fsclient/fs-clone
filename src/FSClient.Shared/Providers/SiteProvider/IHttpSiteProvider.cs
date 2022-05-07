namespace FSClient.Shared.Providers
{
    using System.Net.Http;

    using FSClient.Shared.Helpers.TimeSpanSemaphore;

    public interface IHttpSiteProvider : ISiteProvider
    {
        HttpClient HttpClient { get; }
        HttpClientHandler Handler { get; }
        ITimeSpanSemaphore RequestSemaphore { get; }
    }
}

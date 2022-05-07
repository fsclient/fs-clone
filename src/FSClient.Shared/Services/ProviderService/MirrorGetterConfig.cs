namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public record MirrorGetterConfig(IReadOnlyCollection<Uri> Mirrors)
    {
        private static readonly Func<HttpResponseMessage, CancellationToken, ValueTask<bool>> defaultValidator = new Func<HttpResponseMessage, CancellationToken, ValueTask<bool>>(
            (response, _) => new ValueTask<bool>(response.IsSuccessStatusCode));

        public HttpMethod HttpMethod { get; init; } = HttpMethod.Get;

        public IReadOnlyDictionary<string, string>? AdditionalHeaders { get; init; }

        public ProviderMirrorCheckingStrategy MirrorCheckingStrategy { get; init; } = ProviderMirrorCheckingStrategy.Parallel;

        public Uri? HealthCheckRelativeLink { get; init; }

        public Func<HttpResponseMessage, CancellationToken, ValueTask<bool>> Validator { get; init; } = defaultValidator;

        public Func<Uri?, CancellationToken, ValueTask<Uri?>>? MirrorFinder { get; init; }

        public HttpClientHandler? Handler { get; init; }
    }
}

namespace FSClient.Shared.Services
{
    using System.Collections.Generic;
    using System.Linq;

    public record ApplicationGlobalSettings
    {
        public IEnumerable<ProviderConfig> ProviderConfigs { get; init; } = Enumerable.Empty<ProviderConfig>();

        public IReadOnlyDictionary<DistributionType, LatestVersionInfo> LatestVersionPerDistributionType { get; init; } = new Dictionary<DistributionType, LatestVersionInfo>();
    }
}

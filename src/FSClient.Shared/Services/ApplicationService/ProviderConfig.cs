namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public record ProviderConfig
    {
        public ProviderConfig(Site site,
            ProviderRequirements? requirements = null,
            ProviderMirrorCheckingStrategy? mirrorCheckingStrategy = null,
            int? priority = null,
            bool? isVisibleToUser = null,
            bool? enforceDisabled = null,
            bool? isEnabledByDefault = null,
            bool? canBeMain = null,
            Uri? healthCheckRelativeLink = null,
            IEnumerable<Uri>? mirrors = null,
            IReadOnlyDictionary<string, string?>? properties = null)
        {
            Site = site;
            Requirements = requirements;
            MirrorCheckingStrategy = mirrorCheckingStrategy;
            Priority = priority;
            IsVisibleToUser = isVisibleToUser;
            EnforceDisabled = enforceDisabled;
            IsEnabledByDefault = isEnabledByDefault;
            CanBeMain = canBeMain;
            HealthCheckRelativeLink = healthCheckRelativeLink;
            Mirrors = mirrors;
            Properties = properties;
        }

        public Site Site { get; init; }

        public bool? IsVisibleToUser { get; init; }

        public bool? EnforceDisabled { get; init; }

        public bool? IsEnabledByDefault { get; init; }

        public bool? CanBeMain { get; init; }

        public Uri? HealthCheckRelativeLink { get; init; }

        public IEnumerable<Uri>? Mirrors { get; init; }

        public ProviderRequirements? Requirements { get; init; }

        public ProviderMirrorCheckingStrategy? MirrorCheckingStrategy { get; init; }

        public int? Priority { get; init; }

        public IReadOnlyDictionary<string, string?>? Properties { get; init; }
    }
}

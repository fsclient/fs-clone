namespace FSClient.Shared.Services
{
    using System;

    public record LatestVersionInfo(
        string Version,
        Uri? FallbackInstallPage);
}

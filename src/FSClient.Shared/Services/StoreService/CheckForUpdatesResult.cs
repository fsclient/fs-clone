namespace FSClient.Shared.Services
{
    using System;

    public record CheckForUpdatesResult(
        CheckForUpdatesResultType ResultType,
        Version? Version = null,
        Uri? InstallUpdateLink = null);
}

namespace FSClient.Shared.Services
{
    using System;

    public interface IAppInformation : ILogState
    {
        string DeviceManufacturer { get; }
        string DeviceModel { get; }
        DeviceFamily DeviceFamily { get; }

        Version SystemVersion { get; }
        string SystemArchitecture { get; }

        bool IsUpdated { get; }
        Version ManifestVersion { get; }
        Version AssemblyVersion { get; }
        string DisplayName { get; }
        bool IsDevBuild { get; }

        (ulong Current, ulong Total) MemoryUsage();
    }
}

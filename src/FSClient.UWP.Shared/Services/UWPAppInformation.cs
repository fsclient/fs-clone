namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;

    using Windows.ApplicationModel;
    using Windows.Security.ExchangeActiveSyncProvisioning;
    using Windows.Storage;
    using Windows.System;
    using Windows.System.Profile;
#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    using FSClient.Shared.Services;

    public sealed class UWPAppInformation : IAppInformation
    {
        public static readonly UWPAppInformation Instance = new UWPAppInformation();

        private static readonly Lazy<(string manufacturer, string model)> deviceInfoFactory
            = new Lazy<(string manufacturer, string model)>(GetDeviceInfo);

        private static readonly Lazy<Version> versionFactory
            = new Lazy<Version>(GetApplicationVersion);

        private static readonly Lazy<Version> deviceVersionFactory
            = new Lazy<Version>(GetDeviceVersion);

        private static readonly Lazy<bool> isUpdatedFactory
            = new Lazy<bool>(GetIsUpdated);

        private static readonly Lazy<DeviceFamily> deviceFamilyFactory
            = new Lazy<DeviceFamily>(GetDeviceFamily);

        public string DeviceManufacturer => deviceInfoFactory.Value.manufacturer;

        public string DeviceModel => deviceInfoFactory.Value.model;

        public DeviceFamily DeviceFamily => deviceFamilyFactory.Value;

        public string SystemArchitecture => Package.Current.Id.Architecture.ToString();

        public Version SystemVersion => deviceVersionFactory.Value;

        public Version ManifestVersion => versionFactory.Value;

        public Version AssemblyVersion => Version.Parse(ThisAssembly.AssemblyFileVersion);

        public bool IsUpdated => isUpdatedFactory.Value;

#if DEV_BUILD
        public bool IsDevBuild => true;
#else
        public bool IsDevBuild => false;
#endif

        public string DisplayName => Package.Current.DisplayName;

        public bool IsXYModeEnabled
        {
            get => Application.Current.RequiresPointerMode == ApplicationRequiresPointerMode.WhenRequested;
            set
            {
                try
                {
                    Application.Current.RequiresPointerMode = value
                        ? ApplicationRequiresPointerMode.WhenRequested
                        : ApplicationRequiresPointerMode.Auto;
                }
                catch (Exception ex)
                {
                    if (Logger.Initialized)
                    {
                        Logger.Instance.LogWarning(ex);
                    }
                }
            }
        }

        public (ulong Current, ulong Total) MemoryUsage()
        {
            ulong appMemoryUsageUlong = 0, appMemoryUsageLimitUlong = 0;
            try
            {
                appMemoryUsageUlong = MemoryManager.AppMemoryUsage / 1024 / 1024;
                appMemoryUsageLimitUlong = MemoryManager.AppMemoryUsageLimit / 1024 / 1024;
            }
            catch (Exception ex)
            {
                if (Logger.Initialized)
                {
                    Logger.Instance.LogWarning(ex);
                }
            }

            return (appMemoryUsageUlong, appMemoryUsageLimitUlong);
        }

        private static DeviceFamily GetDeviceFamily()
        {
            return AnalyticsInfo.VersionInfo.DeviceFamily switch
            {
                "Windows.Mobile" => DeviceFamily.Mobile,
                "Windows.Desktop" => DeviceFamily.Desktop,
                "Windows.Team" => DeviceFamily.Team,
                "Windows.IoT" => DeviceFamily.IoT,
                "Windows.Holographic" => DeviceFamily.Holographic,
                "Windows.Xbox" => DeviceFamily.Xbox,
                _ => DeviceFamily.Unknown,
            };
        }

        private static Version GetDeviceVersion()
        {
            var sv = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            var v = ulong.Parse(sv);
            var v1 = (v & 0xFFFF000000000000L) >> 48;
            var v2 = (v & 0x0000FFFF00000000L) >> 32;
            var v3 = (v & 0x00000000FFFF0000L) >> 16;
            var v4 = v & 0x000000000000FFFFL;
            return new Version((int)v1, (int)v2, (int)v3, (int)v4);
        }

        private static Version GetApplicationVersion()
        {
            var ver = Package.Current.Id.Version;
            return new Version(ver.Major, ver.Minor, ver.Build, ver.Revision);
        }

        private static bool GetIsUpdated()
        {
            var current = versionFactory.Value;
            _ = Version.TryParse(
                ApplicationData.Current.LocalSettings.Values["Version"]?.ToString(),
                out var last);

            ApplicationData.Current.LocalSettings.Values["Version"] = current.ToString(3);

            return last != null
                   && (last.Major != current.Major
                       || last.Minor != current.Minor
                       || last.Build != current.Build);
        }

        private static (string manufacturer, string model) GetDeviceInfo()
        {
            string deviceManufacturer, deviceModel;
            try
            {
                var eas = new EasClientDeviceInformation();
                deviceManufacturer = eas.SystemManufacturer;
                deviceModel = eas.SystemProductName;
            }
            catch (Exception ex)
            {
                deviceManufacturer = "Unknown";
                deviceModel = "Unknown";

                if (Logger.Initialized)
                {
                    Logger.Instance.LogWarning(ex);
                }
            }

            if (deviceManufacturer?.StartsWith("System", StringComparison.Ordinal) ?? true)
            {
                deviceManufacturer = "PC";
                deviceModel = string.Empty;
            }

            return (deviceManufacturer, deviceModel);
        }

        public override string ToString()
        {
            return
                $@"Version = {ThisAssembly.AssemblyInformationalVersion}
Windows = {SystemVersion}
DeviceFamily = {DeviceFamily}";
        }

        public IDictionary<string, string> GetLogProperties(bool verbose)
        {
            var props = new Dictionary<string, string>
            {
                ["Version"] = ManifestVersion.ToString(3),
                ["Windows"] = SystemVersion.ToString(3),
                ["DeviceFamily"] = DeviceFamily.ToString(),
                ["SystemArchitecture"] = SystemArchitecture
            };

            if (verbose)
            {
                props["AssemblyVersion"] = AssemblyVersion.ToString();
                //props["DeviceManufacturer"] = DeviceManufacturer;
                //props["DeviceModel"] = DeviceModel;
                props["MemoryUsage"] = MemoryUsage().ToString();
            }

            return props;
        }
    }
}

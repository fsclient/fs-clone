namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.ApplicationModel;
    using Windows.Foundation.Metadata;

    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    public partial class StoreService : IStoreService
    {
        private readonly ILogger logger;
        private readonly IApplicationService applicationService;
        private readonly IAppInformation appInformation;
        private readonly Lazy<Uri?> appInstallerInfoUriLazy;

        public StoreService(
            ILogger logger,
            IApplicationService applicationService,
            IAppInformation appInformation)
        {
            this.logger = logger;
            this.applicationService = applicationService;
            this.appInformation = appInformation;

            appInstallerInfoUriLazy = new Lazy<Uri?>(() =>
            {
                if (ApiInformation.IsMethodPresent(typeof(Package).FullName,
                    nameof(Package.Current.GetAppInstallerInfo)))
                {
                    return Package.Current.GetAppInstallerInfo()?.Uri;
                }

                return null;
            });

#if DEBUG
            DistributionType = DistributionType.Debug;
#else
            DistributionType = GetNonDebugDistributionType();
#endif
        }

        public DistributionType DistributionType { get; }

        public async Task<CheckForUpdatesResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            if (appInformation.IsDevBuild)
            {
                return new CheckForUpdatesResult(CheckForUpdatesResultType.NotSupported);
            }

            try
            {
                var settings = await applicationService.GetApplicationGlobalSettingsAsync(cancellationToken)
                    .ConfigureAwait(false);
                LatestVersionInfo? versionInfo = null;
                settings.LatestVersionPerDistributionType?.TryGetValue(DistributionType, out versionInfo);

                var updateAvailable = await CheckForUpdatesInternal(versionInfo);

                if (updateAvailable != CheckForUpdatesResultType.Available)
                {
                    return new CheckForUpdatesResult(updateAvailable);
                }

                Version? version = null;
                if (versionInfo != null)
                {
                    _ = Version.TryParse(versionInfo.Version, out version);
                }

                if (DistributionType == DistributionType.Store)
                {
                    var storeLink = new Uri("ms-windows-store://pdp/?ProductId=" + Package.Current.Id.ProductId);
                    return new CheckForUpdatesResult(updateAvailable, version, storeLink);
                }
                else if (appInstallerInfoUriLazy.Value is Uri appInstallerInfoUri)
                {
                    return new CheckForUpdatesResult(updateAvailable, version,
                        WrapAppInstallerLink(appInstallerInfoUri));
                }
                else if (versionInfo?.FallbackInstallPage is Uri fallbackInstallPage)
                {
                    return new CheckForUpdatesResult(updateAvailable, version,
                        WrapAppInstallerLink(fallbackInstallPage));
                }

                return new CheckForUpdatesResult(CheckForUpdatesResultType.NotSupported);
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
                return new CheckForUpdatesResult(CheckForUpdatesResultType.UnknownError);
            }

            static Uri WrapAppInstallerLink(Uri link)
            {
                if (link.OriginalString.Contains(".appinstaller"))
                {
                    return new Uri("ms-appinstaller:?source=" + link.OriginalString);
                }
                else
                {
                    return link;
                }
            }
        }

        private async Task<CheckForUpdatesResultType> CheckForUpdatesInternal(LatestVersionInfo? versionInfo)
        {
            if (versionInfo?.Version is { } newVersionStr
                && Version.TryParse(newVersionStr, out var newVersion)
                && appInformation.ManifestVersion is { } currentVersion
                && newVersion > currentVersion)
            {
                return CheckForUpdatesResultType.Available;
            }
            else if (DistributionType != DistributionType.Sideloaded
                     && ApiInformation.IsMethodPresent(typeof(Package).FullName,
                         nameof(Package.CheckUpdateAvailabilityAsync)))
            {
                var updateAvailability = await Package.Current.CheckUpdateAvailabilityAsync();

                if (updateAvailability.ExtendedError is Exception exception)
                {
                    logger.LogWarning(exception);
                }

                if (updateAvailability.Availability != PackageUpdateAvailability.Available
                    && updateAvailability.Availability != PackageUpdateAvailability.Required)
                {
                    return updateAvailability.Availability switch
                    {
                        // Not an app installer package
                        PackageUpdateAvailability.Unknown => CheckForUpdatesResultType.NotSupported,
                        PackageUpdateAvailability.NoUpdates => CheckForUpdatesResultType.NoUpdates,
                        PackageUpdateAvailability.Error => CheckForUpdatesResultType.UnknownError,
                        _ => throw new NotImplementedException(
                            $"{nameof(PackageUpdateAvailability)}.{updateAvailability.Availability} is not implemented.")
                    };
                }
                else
                {
                    return CheckForUpdatesResultType.Available;
                }
            }
            else
            {
                return CheckForUpdatesResultType.NoUpdates;
            }
        }

        private DistributionType GetNonDebugDistributionType()
        {
            if (appInstallerInfoUriLazy.Value != null)
            {
                return DistributionType.AppInstaller;
            }
            else if (!ApiInformation.IsPropertyPresent(typeof(Package).FullName, nameof(Package.Current.SignatureKind))
                     || Package.Current.SignatureKind == PackageSignatureKind.Store)
            {
                return DistributionType.Store;
            }
            else
            {
                return DistributionType.Sideloaded;
            }
        }
    }
}

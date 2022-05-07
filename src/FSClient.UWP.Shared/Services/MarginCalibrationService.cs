namespace FSClient.UWP.Shared.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Views.Dialogs;

    public class MarginCalibrationService
    {
        private const string IsMarginCalibratedKey = nameof(IsMarginCalibratedKey);

        private static readonly LazyDialog<MarginCalibrationDialog> calibrationDialog =
            new LazyDialog<MarginCalibrationDialog>();

        private readonly IAppInformation appInformation;
        private readonly ISettingService settingService;

        public MarginCalibrationService(
            IAppInformation appInformation,
            ISettingService settingService)
        {
            this.appInformation = appInformation;
            this.settingService = settingService;
        }

        public Task EnsureMarginCalibratedAsync(bool force, CancellationToken cancellationToken)
        {
            if (!force)
            {
                if (appInformation.DeviceFamily != DeviceFamily.Xbox)
                {
                    return Task.FromResult(false);
                }

                var wasCalibrated = settingService.GetSetting(Settings.StateSettingsContainer, IsMarginCalibratedKey,
                    false, SettingStrategy.Local);
                if (wasCalibrated)
                {
                    return Task.FromResult(false);
                }
            }

            return EnsureMarginCalibratedInternalAsync();

            async Task EnsureMarginCalibratedInternalAsync()
            {
                await calibrationDialog.ShowAsync(cancellationToken).ConfigureAwait(false);
                settingService.SetSetting(Settings.StateSettingsContainer, IsMarginCalibratedKey, true,
                    SettingStrategy.Local);
            }
        }
    }
}

namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    using Windows.Media.Core;

    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    public class SystemFeaturesService : ISystemFeaturesService
    {
        private readonly ILogger logger;

        private static readonly Uri mediaFeaturePackLink =
            new Uri("https://www.microsoft.com/en-us/software-download/mediafeaturepack");

        private static readonly Uri mpegExtensionLink = new Uri("ms-windows-store://pdp?productId=9N95Q1ZZPMH4");

        public SystemFeaturesService(
            ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<IEnumerable<MissedSystemFeature>> GetMissedFeaturesAsync()
        {
            var features = new List<MissedSystemFeature>();
            if (UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Desktop
                && !TryGetIsMediaFoundationInstalled())
            {
                features.Add(new MissedSystemFeature("Media Feature Pack", mediaFeaturePackLink));
            }

            if (!await TryGetIsMPEG2VideoExtensionInstalledAsync().ConfigureAwait(false))
            {
                features.Add(new MissedSystemFeature("MPEG-2 Video Extension", mpegExtensionLink));
            }

            return features;
        }

        private bool TryGetIsMediaFoundationInstalled()
        {
            try
            {
                var result = NativeMethods.MFStartup() == NativeMethods.S_OK;
                if (result)
                {
                    _ = NativeMethods.MFShutdown();
                }

                return result;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
                return false;
            }
        }

        private async Task<bool> TryGetIsMPEG2VideoExtensionInstalledAsync()
        {
            try
            {
                var query = new CodecQuery();
                var results = await query.FindAllAsync(CodecKind.Video, CodecCategory.Decoder,
                    CodecSubtypes.VideoFormatMpeg2);
                return results.Any();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
                return false;
            }
        }

        private static class NativeMethods
        {
            public const uint MF_SDK_VERSION = 0x02;
            public const uint MF_API_VERSION = 0x70;
            public const uint MF_VERSION = (MF_SDK_VERSION << 16) | MF_API_VERSION;
            public const uint MFSTARTUP_FULL = 0;

            public const uint S_OK = 0;
            public const uint MF_E_BAD_STARTUP_VERSION = 0xC00D36E3;
            public const uint MF_E_DISABLED_IN_SAFEMODE = 0xC00D36EF;
            public const uint E_NOTIMPL = 0x80004001;

            [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
            public static extern uint MFStartup(uint version = MF_VERSION, uint dwFlags = MFSTARTUP_FULL);


            [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
            public static extern uint MFShutdown();
        }
    }
}

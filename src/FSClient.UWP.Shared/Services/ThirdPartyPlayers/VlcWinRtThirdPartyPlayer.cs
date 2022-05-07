namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.System;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    public class VlcWinRtThirdPartyPlayer : IThirdPartyPlayer
    {
        private const string VlcWinRtBaseProtocol = "vlc://openstream?";
        private const string VlcWinRtPackageName = "VideoLAN.VLC_paz6r1rewnh0a";

        public VlcWinRtThirdPartyPlayer()
        {
            Details = new ThirdPartyPlayerDetails(VlcWinRtPackageName, "VLC WinRT");
        }

        public ThirdPartyPlayerDetails Details { get; }

        public ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            return new ValueTask<bool>(DispatcherHelper.GetForCurrentOrMainView().CheckBeginInvokeOnUI(async () =>
            {
                var result = await Launcher.QueryUriSupportAsync(new Uri(VlcWinRtBaseProtocol),
                        LaunchQuerySupportType.Uri, VlcWinRtPackageName)
                    .AsTask(cancellationToken).ConfigureAwait(false);
                return result == LaunchQuerySupportStatus.Available;
            }));
        }

        public ValueTask<ThirdPartyPlayerOpenResult> OpenVideoAsync(Video video, SubtitleTrack? subtitleTrack,
            CancellationToken cancellationToken)
        {
            return new ValueTask<ThirdPartyPlayerOpenResult>(DispatcherHelper.GetForCurrentOrMainView()
                .CheckBeginInvokeOnUI(async () =>
                {
                    if (!await IsAvailableAsync(cancellationToken).ConfigureAwait(true))
                    {
                        return ThirdPartyPlayerOpenResult.NotAvailable;
                    }

                    var result = ThirdPartyPlayerOpenResult.Success;
                    var args = new Dictionary<string, string?>();

                    if (video.Links.Count > 1
                        || video.CustomHeaders.Any())
                    {
                        result = ThirdPartyPlayerOpenResult.SuccessWithMissedInfo;
                    }

                    args["from"] = "url";
                    args["url"] = video.Links.First().ToString();

                    if (subtitleTrack != null)
                    {
                        args["subs_from"] = "url"; // (or path, or picker)
                        args["subs"] = subtitleTrack.Link.ToString();
                    }

                    var uriToOpen = new Uri(VlcWinRtBaseProtocol + QueryStringHelper.CreateQueryString(args));

                    var successed = await Launcher
                        .LaunchUriAsync(uriToOpen,
                            new LauncherOptions {TargetApplicationPackageFamilyName = VlcWinRtPackageName})
                        .AsTask(cancellationToken)
                        .ConfigureAwait(false);

                    if (!successed)
                    {
                        result = ThirdPartyPlayerOpenResult.FailedUnknown;
                    }

                    return result;
                }));
        }
    }
}

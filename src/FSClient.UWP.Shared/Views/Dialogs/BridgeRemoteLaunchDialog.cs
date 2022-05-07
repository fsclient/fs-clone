namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Services;

    public sealed partial class
        BridgeRemoteLaunchDialog : IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput>
    {
        private readonly IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput> legacyRemoteLaunchDialog;
        private readonly IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput> remoteLaunchDialog;

        public BridgeRemoteLaunchDialog()
        {
            legacyRemoteLaunchDialog = new LegacyRemoteLaunchDialog();
            remoteLaunchDialog = new RemoteLaunchDialog();
        }

        Task<RemoteLaunchDialogOutput> IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput>.ShowAsync(
            RemoteLaunchDialogInput remoteLauncherInput,
            CancellationToken cancellationToken)
        {
            if (!Settings.Instance.PreferLegacyRemoteLaunchDialog
                || (remoteLauncherInput.MediaSource == null
                    && !remoteLauncherInput.UriSourceIsVideo))
            {
                return remoteLaunchDialog.ShowAsync(remoteLauncherInput, cancellationToken);
            }
            else
            {
                return legacyRemoteLaunchDialog.ShowAsync(remoteLauncherInput, cancellationToken);
            }
        }
    }
}

namespace FSClient.Shared.Services
{
    using System;
    using System.Threading.Tasks;

    public interface ILauncherService
    {
        Task<LaunchResult> LaunchUriAsync(Uri uri);
        Task<LaunchResult> LaunchFileAsync(IStorageFile file);
        Task<LaunchResult> LaunchFolderAsync(IStorageFolder folder);
        Task<RemoteLaunchDialogOutput> RemoteLaunchUriAsync(RemoteLaunchDialogInput input);
    }
}

namespace FSClient.Shared.Services
{
    using System.Threading.Tasks;

    public interface IStorageService
    {
        IStorageFolder? LocalFolder { get; }

        IStorageFolder? TempFolder { get; }

        Task<IStorageFile?> OpenFileFromPathAsync(string path);

        Task<IStorageFile?> PickFileAsync(string suggestedName, bool createNew = false);

        Task<IStorageFolder?> PickFolderAsync();

        Task<IStorageFolder?> GetSavedFolderAsync(string token);

        Task<string?> SaveFolderAsync(IStorageFolder folder, string? token = null);

        void ForgetSavedFolder(string token);

        Task<bool> ClearApplicationData();
    }
}

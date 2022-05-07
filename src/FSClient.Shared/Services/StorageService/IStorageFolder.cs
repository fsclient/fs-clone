namespace FSClient.Shared.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IStorageFolder : IStorageItem
    {
        Task<IStorageFile?> GetFileAsync(string fileName);
        Task<IStorageFolder?> GetOrCreateFolderAsync(string folderName);
        Task<IStorageFile?> CreateFileAsync(string fileName, bool replace = false);
        Task<IStorageFile?> OpenOrCreateFileAsync(string fileName);

        Task<ulong?> GetAvaliableSpaceAsync();
        Task<IEnumerable<IStorageItem>> GetItemsAsync();
    }
}

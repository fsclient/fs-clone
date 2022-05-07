namespace FSClient.Shared.Services
{
    using System;
    using System.Threading.Tasks;

    public interface IStorageItem
    {
        string Title { get; }
        string Path { get; }

        bool CanDelete { get; }

        DateTimeOffset? DateCreated { get; }

        Task<IStorageFolder?> GetParentAsync();
        Task<bool> DeleteAsync();
    }
}

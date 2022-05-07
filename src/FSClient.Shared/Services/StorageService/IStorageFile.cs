namespace FSClient.Shared.Services
{
    using System.IO;
    using System.Threading.Tasks;

    public interface IStorageFile : IStorageItem
    {
        bool CanRead { get; }
        bool CanWrite { get; }
        ulong SizeInBytes { get; }

        Task<Stream?> ReadAsync();
        Task<Stream?> OpenForWriteAsync();
        Task<bool> WriteAsync(Stream stream);
    }
}

namespace FSClient.Shared.Services
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    using File = FSClient.Shared.Models.File;

    public interface IShareService
    {
        bool IsSupported { get; }

        Task<bool> ShareItemAsync(ItemInfo item);

        Task<bool> ShareFrameAsync(Stream stream, TimeSpan position, File file);

        Task<bool> CopyTextToClipboardAsync(string text);
    }
}

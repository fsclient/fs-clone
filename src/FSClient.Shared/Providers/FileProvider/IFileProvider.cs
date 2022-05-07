namespace FSClient.Shared.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    public interface IFileProvider : IProviderWithRequirements
    {
        bool ProvideOnline { get; }
        bool ProvideTorrent { get; }
        bool ProvideTrailers { get; }

        void InitForItems(IEnumerable<ItemInfo> items);

        Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken cancellationToken);
        Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken);
        Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken);
    }
}

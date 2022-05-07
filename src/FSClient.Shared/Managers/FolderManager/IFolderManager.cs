namespace FSClient.Shared.Managers
{
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    /// <summary>
    /// Folders manager.
    /// </summary>
    public interface IFolderManager
    {
        /// <summary>
        /// Reads history to find latest opened node and preloads folder with it.
        /// Will fetch folders from root to latest folder.
        /// </summary>
        /// <param name="item">Item to fetch from history.</param>
        /// <param name="historyItem">History item to simplify query to history.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple of folder and fetched history item. Both null, if failed.</returns>
        Task<(IFolderTreeNode? folder, HistoryItem? historyItem)> GetFolderFromHistoryAsync(
            ItemInfo item, HistoryItem? historyItem, CancellationToken cancellationToken);

        /// <summary>
        /// Searches item on online-files provider and fetches root folder from it.
        /// </summary>
        /// <param name="item">Base item, which should be founded in files providers.</param>
        /// <param name="site">Files provider site. Can be specific site, <see cref="Site.All"/> or <see cref="Site.Any"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple of folder and opening result returned from provider. Folder is null, if failed.</returns>
        Task<(Folder? Folder, ProviderResult Result)> GetFilesRootAsync(
            ItemInfo item, Site site, CancellationToken cancellationToken);

        /// <summary>
        /// Searches item on torrent-files provider and fetches root folder from it.
        /// </summary>
        /// <param name="item">Base item, which should be founded in files providers.</param>
        /// <param name="site">Files provider site. Can be specific site, <see cref="Site.All"/> or <see cref="Site.Any"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple of folder and opening result returned from provider. Folder is null, if failed.</returns>
        Task<(Folder? Folder, ProviderResult Result)> GetTorrentsRootAsync(
            ItemInfo item, Site site, CancellationToken cancellationToken);

        /// <summary>
        /// Opens folder from files providers, which owns it, and fills <see cref="Folder.ItemsSource"/>.
        /// </summary>
        /// <param name="folder">Folder to open.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Folder opening result returned from provider.</returns>
        Task<ProviderResult> OpenFolderAsync(
            IFolderTreeNode folder, CancellationToken cancellationToken);

        /// <summary>
        /// Reload folder.
        /// Will fetch folders from root to reloaded.
        /// </summary>
        /// <param name="folder">Folder to reload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Relaoded tree node. Null, if failed.</returns>
        Task<IFolderTreeNode?> ReloadFolderAsync(
            IFolderTreeNode folder, CancellationToken cancellationToken);
    }
}

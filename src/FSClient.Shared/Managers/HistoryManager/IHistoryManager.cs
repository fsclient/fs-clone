namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    public interface IHistoryManager
    {
        event EventHandler<HistoryChangedEventArgs>? FilesHistoryChanged;

        event EventHandler<ItemHistoryChangedEventArgs>? ItemsHistoryChanged;

        ValueTask<bool> IsInHistoryAsync(ItemInfo item);

        Task<int> UpsertAsync(IEnumerable<HistoryItem> item);

        Task<int> RemoveAsync(IEnumerable<HistoryItem> items);

        IAsyncEnumerable<HistoryItem> GetHistory(bool ensureItems = false);

        Task LoadPositionToNodeAsync(ITreeNode node);

        Task<TTreeNode?> GetLastViewedFolderChildAsync<TTreeNode>(IFolderTreeNode folder, HistoryItem? item = null)
            where TTreeNode : class, ITreeNode;
    }
}

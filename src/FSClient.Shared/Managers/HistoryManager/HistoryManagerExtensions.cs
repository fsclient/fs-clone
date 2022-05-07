namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;

    public static class HistoryManagerExtensions
    {
        public static Task<int> UpsertAsync<TTreeNode>(
            this IHistoryManager historyManager,
            IEnumerable<TTreeNode> nodes) where TTreeNode : class, ITreeNode
        {
            return historyManager
                .UpsertAsync(nodes
                .Select(node =>
                {
                    var itemInfo = node?.ItemInfo;
                    var file = node as File;

                    if (itemInfo?.SiteId == null
                        || node?.Id == null
                        || file?.IsTrailer == true)
                    {
                        return null;
                    }

                    var historyItem = new HistoryItem(itemInfo, new HistoryNode(node.Id, node.Position))
                    {
                        Season = node.Season,
                        Episode = node.Episode,
                        AddTime = DateTimeOffset.Now,
                        IsTorrent = node.IsTorrent
                    };

                    if (node.Parent is IFolderTreeNode parentFolder)
                    {
                        var parentNode = parentFolder.AsHistoryNode();
                        if (parentNode == null && file != null && parentFolder.Site.IsSpecial)
                        {
                            parentNode = new HistoryNode(file.Site.Value, 0);
                        }

                        historyItem.Node!.Parent = parentNode;
                    }
                    return historyItem;
                })
                .Where(node => node != null)!);
        }

        public static IAsyncEnumerable<ItemInfo> GetItemsHistory(this IHistoryManager historyManager, bool ensureItems = false)
        {
            return historyManager.GetHistory(ensureItems)
                .Select(i => i.ItemInfo)
                .Distinct();
        }
    }
}

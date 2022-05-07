namespace FSClient.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Shared.Models;

    public static class NodeHelper
    {
        public static IEnumerable<TNode> GetDeepNodes<TNode>(this IEnumerable<ITreeNode> nodes)
            where TNode : ITreeNode
        {
            return nodes
                .Select(node => node switch
                {
                    TNode t => new[] { t },
                    IFolderTreeNode f => GetDeepNodes<TNode>(f.ItemsSource),
                    _ => Enumerable.Empty<TNode>(),
                })
                .SelectMany(n => n);
        }

        public static IEnumerable<TParentType> ParentsEnumerable<TParentType>(this ITreeNode? node)
            where TParentType : ITreeNode
        {
            while (node?.Parent is TParentType parent)
            {
                yield return parent;
                node = parent;
            }
        }

        public static void PrepareNodes(this IEnumerable<ITreeNode> source, ItemInfo? item = null, IFolderTreeNode? parent = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            foreach (var node in source)
            {
                node.ItemInfo = item;
                node.Parent = parent;
                node.IsTorrent = parent?.IsTorrent ?? node.IsTorrent;
                if (node is IFolderTreeNode folder
                    && folder.Count > 0)
                {
                    PrepareNodes(folder.ItemsSource, item, folder);
                }
            }
        }

        public static HistoryNode? AsHistoryNode(this ITreeNode treeNode, bool recalculateCallerPosition = true)
        {
            if (treeNode == null)
            {
                throw new ArgumentNullException(nameof(treeNode));
            }

            var historyNodes = Enumerable.Empty<HistoryNode>();
            if (treeNode is File file
                && file.Id != null)
            {
                historyNodes = historyNodes.Concat(new[]
                {
                    new HistoryNode(file.Id, file.Position)
                });
            }

            var folder = treeNode as IFolderTreeNode ?? treeNode.Parent;
            if (folder != null)
            {
                var parents = new[] { folder }.Concat(folder.ParentsEnumerable<IFolderTreeNode>())
                    .GroupBy(f => f.Id)
                    .ToDictionary(g => g.Key, f => f.AsEnumerable());
                historyNodes = folder
                    .GetIDsStack()
                    .Reverse()
                    .Select(id =>
                    {
                        var position = 0f;
                        if (parents.TryGetValue(id, out var parentsPerId)
                            && (id != treeNode.Id || recalculateCallerPosition))
                        {
                            foreach (var parent in parentsPerId)
                            {
                                parent.Position = parent.CalculatePosition();
                                position = Math.Max(position, parent.Position);
                            }
                        }

                        return (id, position);
                    })
                    .Select(t => new HistoryNode(t.id, t.position))
                    .Reverse()
                    .Concat(historyNodes);
            }
            return historyNodes
                .Aggregate((HistoryNode?)null, (parent, child) => { child.Parent = parent; return child; });
        }
    }
}

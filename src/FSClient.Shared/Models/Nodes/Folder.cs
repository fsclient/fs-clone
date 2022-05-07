namespace FSClient.Shared.Models
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Mvvm;

    /// <summary>
    /// Folder tree node
    /// </summary>
    public class Folder : TreeNodeBase, IFolderTreeNode, IEquatable<Folder>
    {
        private readonly List<ITreeNode> itemsSource;

        private readonly ContextSafeEvent<NotifyCollectionChangedEventHandler> collectionChangedEvent;

        [Obsolete("Use .ctor with FolderType")]
        public Folder(Site site, string id, PositionBehavior positionBehavior)
            : this(site, id, FolderType.Unknown, positionBehavior)
        {

        }

        public Folder(Site site, string id, FolderType folderType, PositionBehavior positionBehavior)
            : base(site, id)
        {
            FolderType = folderType;
            PositionBehavior = positionBehavior;
            itemsSource = new List<ITreeNode>();

            collectionChangedEvent = new ContextSafeEvent<NotifyCollectionChangedEventHandler>();
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add => collectionChangedEvent.Register(value);
            remove => collectionChangedEvent.Unregister(value);
        }

        public FolderType FolderType { get; }

        public PositionBehavior PositionBehavior { get; }

        public IReadOnlyList<ITreeNode> ItemsSource => itemsSource;

        public bool CanSetPosition => !string.IsNullOrEmpty(Id);

        public int Count => itemsSource.Count;

        public string? Details { get; set; }

        public string? PlaceholderText { get; set; }

        public bool IsLoading
        {
            get => Get(false);
            set => Set(value);
        }

        public void Clear()
        {
            itemsSource.Clear();
        }

        public void Add(ITreeNode item)
        {
            AddRange(new[] { item });
        }

        public void AddRange(IEnumerable<ITreeNode> range)
        {
            var items = range.ToArray();

            items.PrepareNodes(ItemInfo, this);
            itemsSource.AddRange(items);

            OnPropertyChanged(nameof(Count));
            collectionChangedEvent.Invoke(handler =>
                handler.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items)));
        }

        public Stack<string> GetIDsStack(bool ignoreEmptyIDs = true)
        {
            var ids = new Stack<string>(16);

            if (!ignoreEmptyIDs
                || !string.IsNullOrEmpty(Id))
            {
                ids.Push(Id!);
            }

            var providerSite = Site;

            foreach (var parent in this.ParentsEnumerable<IFolderTreeNode>())
            {
                if (!ignoreEmptyIDs
                    || !string.IsNullOrWhiteSpace(parent.Id))
                {
                    ids.Push(parent.Id!);
                    if (!parent.Site.IsSpecial)
                    {
                        providerSite = parent.Site;
                    }
                }
            }

            if (!providerSite.IsSpecial
                && (ids.Count == 0
                    || ids.Peek() != providerSite.Value))
            {
                ids.Push(providerSite.Value);
            }

            return ids;
        }

        public float CalculatePosition()
        {
            if (itemsSource.Count == 0)
            {
                return 0;
            }

            return PositionBehavior switch
            {
                PositionBehavior.Max => itemsSource.Max(f => f.Position),
                PositionBehavior.Average => itemsSource.Sum(f => f.Position) / itemsSource.Count,
                _ => 0,
            };
        }

        public bool Equals(Folder other)
        {
            if (string.IsNullOrEmpty(Id))
            {
                return ReferenceEquals(this, other);
            }

            return Site == other?.Site && Id == other?.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is Folder another && Equals(another);
        }

        public override int GetHashCode()
        {
            if (string.IsNullOrEmpty(Id))
            {
                return base.GetHashCode();
            }

            return (Site, Id).GetHashCode();
        }

        public override string ToString()
        {
            return $"{Site}: {Id}" + (Count > 0 ? " Count: " + Count : "");
        }

        #region IEnumerable
        public IEnumerator<ITreeNode> GetEnumerator()
        {
            return itemsSource.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return itemsSource.GetEnumerator();
        }
        #endregion
    }
}

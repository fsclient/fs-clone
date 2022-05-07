namespace FSClient.Shared.Models
{
    using System.Collections.Generic;
    using System.Collections.Specialized;

    /// <summary>
    /// Folder tree node
    /// </summary>
    public interface IFolderTreeNode : ITreeNode, IEnumerable<ITreeNode>, INotifyCollectionChanged
    {
        /// <summary>
        /// Folder position calculation behaviour. See <see cref="CalculatePosition"/>.
        /// </summary>
        PositionBehavior PositionBehavior { get; }

        /// <summary>
        /// Children source
        /// </summary>
        IReadOnlyList<ITreeNode> ItemsSource { get; }

        /// <summary>
        /// Can manual set position to folder
        /// </summary>
        bool CanSetPosition { get; }

        /// <summary>
        /// Children count
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Node short details
        /// </summary>
        string? Details { get; set; }

        /// <summary>
        /// Placeholder text, if folder doesn't contain any children
        /// </summary>
        string? PlaceholderText { get; }

        /// <summary>
        /// Is folder children loading
        /// </summary>
        bool IsLoading { get; }

        /// <summary>
        /// Clear folder
        /// </summary>
        void Clear();

        /// <summary>
        /// Add new child
        /// </summary>
        /// <param name="node">Node to add</param>
        void Add(ITreeNode node);

        /// <summary>
        /// Add new children
        /// </summary>
        /// <param name="range">Nodes to add</param>
        void AddRange(IEnumerable<ITreeNode> range);

        /// <summary>
        /// Calculate folder position based on children
        /// </summary>
        /// <returns>Calculated position</returns>
        float CalculatePosition();

        /// <summary>
        /// Generate ids stack from current folder to root
        /// </summary>
        /// <param name="ignoreEmptyIDs">Should empty ids be ignored</param>
        /// <returns>Stack with node ids from current to root</returns>
        Stack<string> GetIDsStack(bool ignoreEmptyIDs = true);
    }
}

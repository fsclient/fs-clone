namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;

    public class HistoryNode : IEquatable<HistoryNode>
    {
#nullable disable
        private HistoryNode()
#nullable restore
        {

        }

        public HistoryNode(string key, float position)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Position = position;
        }

        public string Key { get; private set; }

        public float Position { get; set; }

        public HistoryNode? Parent { get; set; }

        public IEnumerable<HistoryNode> Flatten()
        {
            var node = this;
            do
            {
                yield return node;
                node = node.Parent;
            }
            while (node != null);
        }

        public bool Equals(HistoryNode? other)
        {
            return Key == other?.Key;
        }

        public override bool Equals(object obj)
        {
            return obj is HistoryNode other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Key?.GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            return $"Node: {Key} = {Position} <- {Parent?.Key}";
        }

        public static bool operator ==(HistoryNode? left, HistoryNode? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public static bool operator !=(HistoryNode? left, HistoryNode? right)
        {
            return !(left == right);
        }
    }
}

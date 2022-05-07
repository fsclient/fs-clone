namespace FSClient.Shared.Managers
{
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Shared.Models;

    public class ItemHistoryChangedEventArgs : HistoryChangedEventArgs
    {
        private readonly IEnumerable<ItemInfo> items;

        public ItemHistoryChangedEventArgs(HistoryItemChangedReason reason, IEnumerable<ItemInfo> items) : base(reason)
        {
            this.items = items;
        }

        public IReadOnlyCollection<ItemInfo> Items => items.ToArray();
    }
}

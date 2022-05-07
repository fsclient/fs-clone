namespace FSClient.ViewModels.Items
{
    using System;

    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;

    public class HistoryListItemViewModel : ItemsListItemViewModel
    {
        public HistoryListItemViewModel(
            HistoryItem historyItem,
            IItemManager itemManager)
            : base(historyItem.ItemInfo, Shared.Providers.DisplayItemMode.Normal, itemManager)
        {
            HistoryItem = historyItem ?? throw new ArgumentNullException(nameof(historyItem));
        }

        public HistoryItem HistoryItem { get; }
    }
}

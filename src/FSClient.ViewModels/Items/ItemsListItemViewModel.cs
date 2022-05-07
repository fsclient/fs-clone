namespace FSClient.ViewModels.Items
{
    using System;

    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;

    public class ItemsListItemViewModel : ViewModelBase
    {
        private bool isPreloaded;

        public ItemsListItemViewModel(
            ItemInfo itemInfo,
            DisplayItemMode displayItemMode,
            IItemManager itemManager)
        {
            ItemInfo = itemInfo ?? throw new ArgumentNullException(nameof(itemInfo));
            DisplayItemMode = displayItemMode;

            PreloadItemCommand = new AsyncCommand(
                async (ct) =>
                {
                    IsPreloading = true;
                    try
                    {
                        isPreloaded = await itemManager.PreloadItemAsync(itemInfo, PreloadItemStrategy.Poster, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        IsPreloading = false;
                        OnPropertyChanged(nameof(ItemInfo));
                        PreloadItemCommand!.RaiseCanExecuteChanged();
                    }
                },
                () => !isPreloaded && Settings.CanPreloadItems && itemManager.CanPreload(ItemInfo),
                behaviour: AsyncCommandConflictBehaviour.Skip);
        }

        public AsyncCommand PreloadItemCommand { get; }

        public ItemInfo ItemInfo { get; }

        public DisplayItemMode DisplayItemMode { get; }

        public bool IsPreloading
        {
            get => Get<bool>();
            private set => Set(value);
        }
    }
}

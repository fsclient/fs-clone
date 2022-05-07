namespace FSClient.ViewModels.Abstract
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    using FSClient.Shared.Mvvm;

    public abstract class SelectionViewModel : ViewModelBase
    {
        private readonly SafeObservableCollection<object> selectedItems;
        protected bool selectionItemsChanging;

        protected SelectionViewModel()
        {
            SelectionModeToggleCommand = new Command(() => IsInSelectionMode = !IsInSelectionMode);
            AllSelectedToggleCommand = new Command(() => IsAllSelected = !IsAllSelected, () => IsInSelectionMode);
            selectedItems = new SafeObservableCollection<object>();
            selectedItems.CollectionChanged += SelectedItems_CollectionChanged;
        }

        protected abstract IEnumerable<object> Items { get; }

        public ObservableCollection<object> SelectedItems => selectedItems;

        public bool IsInSelectionMode
        {
            get => Get(false);
            set
            {
                if (Set(value))
                {
                    AllSelectedToggleCommand.RaiseCanExecuteChanged();
                    OnIsInSelectionModeChanged(value);
                }
            }
        }

        public bool IsAnySelected
        {
            get => Get(false);
            set
            {
                if (Set(value))
                {
                    if (!value)
                    {
                        IsAllSelected = false;
                    }
                    OnIsAnySelectedChanged(value);
                }
            }
        }

        public bool IsAllSelected
        {
            get => Get(false);
            set
            {
                if (Set(value))
                {
                    if (value)
                    {
                        selectedItems.AddRange(Items.Where(item => !selectedItems.Contains(item)));
                    }
                    else if (!selectionItemsChanging)
                    {
                        if (selectedItems.Count > 0)
                        {
                            selectedItems.Clear();
                        }
                        IsAnySelected = false;
                    }
                    OnIsAllSelectedChanged(value);
                }
            }
        }

        public Command SelectionModeToggleCommand { get; }
        public Command AllSelectedToggleCommand { get; }

        protected virtual void OnSelectedItemsChanged(IEnumerable<object> newValues)
        {
        }

        protected virtual void OnIsInSelectionModeChanged(bool newValue)
        {
        }

        protected virtual void OnIsAllSelectedChanged(bool newValue)
        {
        }

        protected virtual void OnIsAnySelectedChanged(bool newValue)
        {
        }

        private void SelectedItems_CollectionChanged(object _, object __)
        {
            IsAnySelected = SelectedItems.Count > 0;

            selectionItemsChanging = true;
            IsAllSelected = SelectedItems.Count == Items.Count();
            OnSelectedItemsChanged(SelectedItems);
            selectionItemsChanging = false;
        }
    }
}

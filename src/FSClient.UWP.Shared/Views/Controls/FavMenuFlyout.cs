namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Markup;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Markup;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.UWP.Shared.Services;
    using FSClient.ViewModels;

    [ContentProperty(Name = nameof(AdditionalItems))]
    public class FavMenuFlyout : MenuFlyout
    {
        private readonly Lazy<FavoriteMenuViewModel> viewModelFactory;

        public FavMenuFlyout()
        {
            viewModelFactory = new Lazy<FavoriteMenuViewModel>(() =>
                ViewModelLocator.Current.ResolveViewModel<FavoriteMenuViewModel>());

            Opening += OnOpening;
            Closed += OnClosed;
            AdditionalItems = new List<MenuFlyoutItemBase>();
        }

        public ItemInfo? ItemInfo
        {
            get => (ItemInfo?)GetValue(ItemInfoProperty);
            set => SetValue(ItemInfoProperty, value);
        }

        public static readonly DependencyProperty ItemInfoProperty =
            DependencyProperty.Register(nameof(ItemInfo), typeof(ItemInfo), typeof(FavMenuFlyout),
                new PropertyMetadata(null));

        public IReadOnlyCollection<ItemInfo> ItemInfoSource { get; set; } = new List<ItemInfo>();

        public IList<MenuFlyoutItemBase> AdditionalItems { get; }

        private void OnOpening(object _, object __)
        {
            if (!(ItemInfoSource?.Count > 0)
                && ItemInfo == null)
            {
                return;
            }

            Items.Clear();

            var viewModel = viewModelFactory.Value;
            viewModel.Items = ItemInfoSource?.Count > 0 ? ItemInfoSource : new[] {ItemInfo!};
            viewModel.CheckedKinds.CollectionChanged += CheckedTypes_CollectionChanged;
            viewModel.AvailableKinds.CollectionChanged += AvailableTypes_CollectionChanged;

            var toggles = viewModel
                .AvailableKinds
                .Select(
                    kind => new ToggleMenuFlyoutItem
                    {
                        Text = kind.GetDisplayName(), Tag = kind, IsChecked = viewModel.CheckedKinds.Contains(kind)
                    }
                )
                .ToArray();

            foreach (var toggle in toggles)
            {
                Items.Add(toggle);
                toggle.Click += Toggle_Click;

                if ((FavoriteListKind)toggle.Tag == FavoriteListKind.Favorites)
                {
                    Items.Add(new MenuFlyoutSeparator());
                }
            }

            if (AdditionalItems != null)
            {
                var additionalItems = AdditionalItems.ToArray();
                if (additionalItems.Length > 0)
                {
                    if (Items.Count > 0)
                    {
                        Items.Add(new MenuFlyoutSeparator());
                    }

                    foreach (var item in additionalItems)
                    {
                        Items.Add(item);
                    }
                }
            }
        }

        private void AvailableTypes_CollectionChanged(object _, object __)
        {
            Hide();
        }

        private void OnClosed(object sender, object e)
        {
            var viewModel = viewModelFactory.Value;
            viewModel.CheckedKinds.CollectionChanged -= CheckedTypes_CollectionChanged;
            viewModel.AvailableKinds.CollectionChanged -= AvailableTypes_CollectionChanged;
        }

        private void CheckedTypes_CollectionChanged(object _, object __)
        {
            var viewModel = viewModelFactory.Value;
            foreach (var toggle in Items.OfType<ToggleMenuFlyoutItem>())
            {
                var kind = (FavoriteListKind)toggle.Tag;
                toggle.IsChecked = viewModel.CheckedKinds.Contains(kind);
            }
        }

        private async void Toggle_Click(object sender, object _)
        {
            var viewModel = viewModelFactory.Value;

            var toggle = (ToggleMenuFlyoutItem)sender;
            var kind = (FavoriteListKind)toggle.Tag;

            if (toggle.IsChecked)
            {
                await viewModel.CheckKindCommand.ExecuteAsync(kind).ConfigureAwait(false);
            }
            else
            {
                await viewModel.UncheckKindCommand.ExecuteAsync(kind).ConfigureAwait(false);
            }
        }
    }
}

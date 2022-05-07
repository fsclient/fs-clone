namespace FSClient.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Comparers;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;
    using FSClient.Shared.Services.Specifications;
    using FSClient.ViewModels.Abstract;
    using FSClient.ViewModels.Items;
    using FSClient.ViewModels.Pages;

    public class FavoriteViewModel : SelectionViewModel
    {
        private ItemInfoTreeNodeSpecification? itemInfoTreeNodeSpecification;
        private bool favoritesUpdated = true;

        private readonly IFavoriteManager favoriteManager;
        private readonly IHistoryManager historyManager;
        private readonly IItemManager itemManager;
        private readonly ISettingService settingService;

        public FavoriteViewModel(
            IItemManager itemManager,
            IFavoriteManager favoriteManager,
            IHistoryManager historyManager,
            ISettingService settingService)
        {
            this.favoriteManager = favoriteManager;
            this.historyManager = historyManager;
            this.itemManager = itemManager;
            this.settingService = settingService;

            favoriteManager.FavoritesChanged += (s, a) =>
            {
                favoritesUpdated = true;
            };

            UpdateCommand = new AsyncCommand(_ => UpdatePagesAsync(false));
        }

        protected override IEnumerable<object> Items => CurrentPage?.Content?
            .SelectMany(g => g is IGrouping<string, object> group
                ? group.OfType<object>()
                : new[] { g }) ?? Enumerable.Empty<object>();

        public IReadOnlyList<FavoritesTypePageViewModel> PageModels
        {
            get => Get<IReadOnlyList<FavoritesTypePageViewModel>>(Array.Empty<FavoritesTypePageViewModel>);
            private set
            {
                var prevIndex = Get(() => settingService.GetSetting(Settings.StateSettingsContainer, "FavPage", 0), nameof(CurrentPageIndex));
                if (Set(value))
                {
                    CurrentPageIndex = prevIndex;
                    OnPropertyChanged(nameof(CurrentPageIndex));
                    OnPropertyChanged(nameof(CurrentPage));
                }
            }
        }

        public FavoritesTypePageViewModel? CurrentPage => PageModels.Skip(CurrentPageIndex).FirstOrDefault();

        public int CurrentPageIndex
        {
            get
            {
                var index = Get(() => settingService.GetSetting(Settings.StateSettingsContainer, "FavPage", 0));
                if (index >= PageModels.Count)
                {
                    index = PageModels.Count - 1;
                }
                if (index < 0)
                {
                    index = 0;
                }
                return index;
            }
            set
            {
                if (Set(value))
                {
                    if (PageModels.Count > 0)
                    {
                        settingService.SetSetting(Settings.StateSettingsContainer, "FavPage", value);
                    }

                    var totalItemsCount = Items.Count();
                    IsAllSelected = totalItemsCount > 0 && SelectedItems.Count == totalItemsCount;
                    OnPropertyChanged(nameof(CurrentPage));
                }
            }
        }

        public bool GroupItems
        {
            get => Get(() => settingService.GetSetting(Settings.StateSettingsContainer, "GroupItems", true));
            set
            {
                if (Set(value))
                {
                    settingService.SetSetting(Settings.StateSettingsContainer, "GroupItems", value);
                }
            }
        }

        public string SearchRequest
        {
            get => Get(string.Empty);
            set
            {
                if (Set(value))
                {
                    itemInfoTreeNodeSpecification = new ItemInfoTreeNodeSpecification(value);
                }
            }
        }

        public IEnumerable<Section> Sections { get; } = new Section[]
        {
            Section.Any,
            Section.CreateDefault(SectionModifiers.Film),
            Section.CreateDefault(SectionModifiers.Serial),
            Section.CreateDefault(SectionModifiers.Film | SectionModifiers.Cartoon),
            Section.CreateDefault(SectionModifiers.Serial | SectionModifiers.Cartoon),
            Section.CreateDefault(SectionModifiers.TVShow)
        };

        public Section CurrentSection
        {
            get => Get(Section.Any);
            set => Set(value);
        }

        public ICollection<SortType> SortTypes { get; } = new[]
        {
            SortType.Alphabet,
            SortType.LastViewed
        };

        public SortType CurrentSortType
        {
            get => Get(() => settingService.GetSetting(Settings.StateSettingsContainer, "FavoritesSortType", SortTypes.First()));
            set
            {
                if (Set(value))
                {
                    settingService.SetSetting(Settings.StateSettingsContainer, "FavoritesSortType", (int)value);
                }
            }
        }

        public AsyncCommand UpdateCommand { get; }

        #region Updating Methods

        private async Task UpdatePagesAsync(bool force)
        {
            ShowProgress = true;
            try
            {
                if (PageModels == null
                    || PageModels.Count == 0
                    || favoritesUpdated
                    || force)
                {
                    PageModels = favoriteManager.AvailableListKinds
                        .Select(kind =>
                        {
                            var page = new FavoritesTypePageViewModel(kind);

                            async void updateLambda(object _, FavoriteChangedEventArgs args)
                            {
                                if (args.FavoriteListKind == kind)
                                {
                                    await UpdatePageAsync(page).ConfigureAwait(false);
                                }
                            }
                            favoriteManager.FavoritesChanged += updateLambda;

                            return page;
                        })
                        .ToArray();
                }
                await Task.WhenAll(PageModels.Select(UpdatePageAsync)).ConfigureAwait(false);
            }
            finally
            {
                ShowProgress = false;
            }
        }

        private async Task UpdatePageAsync(FavoritesTypePageViewModel pageModel)
        {
            var list = await favoriteManager.GetFavorites(pageModel.FavoriteListKind).Where(Filter)
                .Select(i => new ItemsListItemViewModel(i.ItemInfo, Shared.Providers.DisplayItemMode.Normal, itemManager))
                .ToListAsync();

            switch (CurrentSortType)
            {
                case SortType.Alphabet:
                    list.SortStrings(i => i.ItemInfo.Title ?? string.Empty);
                    break;
                case SortType.LastViewed:
                    var items = await historyManager.GetItemsHistory()
                        .ToListAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                    list = list
                        .OrderBy(i => unchecked((uint)items.IndexOf(i.ItemInfo)))
                        .ThenBy(i => i.ItemInfo.Title, default(NumericStringComparer))
                        .ToList();
                    break;
            }

            if (GroupItems)
            {
                var groups = list
                    .GroupBy(i => GetProperModifier(i.ItemInfo.Section.Modifier))
                    .Select(group => group
                        .GroupBy(i => Section.GetTitleByModifier(GetProperModifier(i.ItemInfo.Section.Modifier))).First())
                    .ToArray();
                if (groups.Length == 1
                    && groups[0].Key == Section.Any.Title)
                {
                    pageModel.Content = list;
                    GroupItems = false;
                }
                else
                {
                    pageModel.Content = groups;
                }
            }
            else
            {
                pageModel.Content = list;
            }
        }

        private bool Filter(FavoriteItem fav)
        {
            if (fav == null)
            {
                return false;
            }

            if (CurrentSection != Section.Any
                && GetProperModifier(fav.ItemInfo.Section.Modifier) != CurrentSection.Modifier)
            {
                return false;
            }

            return itemInfoTreeNodeSpecification?.IsSatisfiedBy(fav.ItemInfo) ?? true;
        }

        private static SectionModifiers GetProperModifier(SectionModifiers modifiers)
        {
            if (modifiers.HasFlag(SectionModifiers.Cartoon | SectionModifiers.Serial))
            {
                return SectionModifiers.Cartoon | SectionModifiers.Serial;
            }
            if (modifiers.HasFlag(SectionModifiers.Cartoon))
            {
                return SectionModifiers.Cartoon | SectionModifiers.Film;
            }
            if (modifiers.HasFlag(SectionModifiers.TVShow))
            {
                return SectionModifiers.TVShow;
            }
            if (modifiers.HasFlag(SectionModifiers.Serial))
            {
                return SectionModifiers.Serial;
            }
            return SectionModifiers.Film;
        }

        #endregion
    }
}

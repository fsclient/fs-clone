namespace FSClient.ViewModels.Pages
{
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.ViewModels.Abstract;
    using FSClient.ViewModels.Items;

    public class SingleItemSearchPageViewModel : SearchPageViewModel
    {
        public SingleItemSearchPageViewModel(ItemInfo itemInfo, IItemManager itemManager, IIncrementalCollectionFactory collectionFactory)
            : base(itemManager, collectionFactory,
                  new SearchPageFilter(
                      new SearchPageParams(itemInfo.Site, itemInfo.Section, DisplayItemMode.Detailed),
                      itemInfo.Link?.OriginalString ?? string.Empty))
        {
            ItemsSource = collectionFactory.Create(new[] { itemInfo }.ToAsyncEnumerable()
                .Select(i => new ItemsListItemViewModel(i, PageParams.DisplayItemMode, itemManager)));
        }

        protected override Task UpdateAsync(bool force, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    public class SearchPageViewModel : BaseSectionPageViewModel<SearchPageFilter, SearchPageParams>
    {
        private readonly IItemManager itemManager;
        private readonly IIncrementalCollectionFactory collectionFactory;
        public SearchPageViewModel(
            IItemManager itemManager,
            IIncrementalCollectionFactory collectionFactory,
            SearchPageFilter pageFilter)
            : base(pageFilter)
        {
            this.itemManager = itemManager;
            this.collectionFactory = collectionFactory;
            PropertyChanged += SectionPageFilter_PropertyChanged;
        }

        public string SearchRequest
        {
            get => Get(string.Empty);
            set => Set(value);
        }

        protected override Task UpdateAsync(bool force, CancellationToken ct)
        {
            if (IsRefreshRequired || force)
            {
                ItemsSource = collectionFactory.Create(itemManager.GetSearchPage(GenerateFilterFromCurrent())
                    .Select(i => new ItemsListItemViewModel(i, PageParams.DisplayItemMode, itemManager)));
                IsRefreshRequired = false;
            }
            return Task.CompletedTask;
        }

        private SearchPageFilter GenerateFilterFromCurrent()
        {
            return new SearchPageFilter(PageParams, SearchRequest ?? string.Empty)
            {
                Year = Year,
                CurrentSortType = CurrentSortType,
                FilterByInHistory = FilterByInHistory,
                FilterByFavorites = FilterByFavorites,
                SelectedTags = SelectedTags
            };
        }

        private void SectionPageFilter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchRequest))
            {
                IsRefreshRequired = true;
            }
        }
    }
}

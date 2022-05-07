namespace FSClient.ViewModels.Pages
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Managers;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.ViewModels.Abstract;
    using FSClient.ViewModels.Items;

    public class SectionPageViewModel : BaseSectionPageViewModel<SectionPageFilter, SectionPageParams>
    {
        private readonly IItemManager itemManager;
        private readonly IIncrementalCollectionFactory collectionFactory;
        public SectionPageViewModel(
            IItemManager itemManager,
            IIncrementalCollectionFactory collectionFactory,
            SectionPageFilter pageFilter)
            : base(pageFilter)
        {
            this.itemManager = itemManager;
            this.collectionFactory = collectionFactory;
        }

        protected override Task UpdateAsync(bool force, CancellationToken ct)
        {
            if (IsRefreshRequired || force)
            {
                ItemsSource = collectionFactory.Create(itemManager.GetSectionPage(GenerateFilterFromCurrent())
                    .Select(i => new ItemsListItemViewModel(i, DisplayItemMode.Normal, itemManager)));
                IsRefreshRequired = false;
            }
            return Task.CompletedTask;
        }

        private SectionPageFilter GenerateFilterFromCurrent()
        {
            return new SectionPageFilter(PageParams)
            {
                Year = Year,
                CurrentSortType = CurrentSortType,
                FilterByInHistory = FilterByInHistory,
                FilterByFavorites = FilterByFavorites,
                SelectedTags = SelectedTags
            };
        }
    }
}

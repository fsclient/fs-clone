namespace FSClient.ViewModels.Abstract
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;

    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.ViewModels.Items;

    public interface ISectionPageViewModel<out TSectionPageParams>
        where TSectionPageParams : SectionPageParams
    {
        TSectionPageParams PageParams { get; }

        Range? Year { get; set; }

        bool FilterByFavorites { get; set; }

        bool FilterByInHistory { get; set; }

        ObservableCollection<TitledTag> SelectedTags { get; }

        SortType? CurrentSortType { get; set; }

        string Caption { get; }

        bool CanSort { get; }
        bool CanFilterByYear { get; }
        bool CanFilterByTags { get; }

        bool CanAnyInteract { get; }
    }

    public abstract class BaseSectionPageViewModel<TSectionPageFilter, TSectionPageParams> : PageViewModel, ISectionPageViewModel<TSectionPageParams>
        where TSectionPageFilter : BaseSectionPageFilter<TSectionPageParams>
        where TSectionPageParams : SectionPageParams
    {
        public BaseSectionPageViewModel(
            TSectionPageFilter pageFilter)
        {
            FilterFromCache = pageFilter;
            PageParams = pageFilter.PageParams;
            SelectedTags = new ObservableCollection<TitledTag>(FilterFromCache.SelectedTags);

            PropertyChanged += SectionPageFilter_PropertyChanged;
            SelectedTags.CollectionChanged += SelectedTags_CollectionChanged;
        }

        protected TSectionPageFilter FilterFromCache { get; }
        protected bool IsRefreshRequired { get; set; } = true;

        public TSectionPageParams PageParams { get; }

        public virtual Range? Year
        {
            get => Get(FilterFromCache.Year);
            set => Set(value);
        }

        public bool FilterByFavorites
        {
            get => Get(FilterFromCache.FilterByFavorites);
            set => Set(value);
        }

        public bool FilterByInHistory
        {
            get => Get(FilterFromCache.FilterByInHistory);
            set => Set(value);
        }

        public ObservableCollection<TitledTag> SelectedTags { get; }

        public virtual SortType? CurrentSortType
        {
            get => Get(() => FilterFromCache.CurrentSortType ?? (PageParams.SortTypes.Count == 0 ? (SortType?)null : PageParams.SortTypes.First()));
            set => Set(value);
        }

        public override string Caption => PageParams.Section.Title;

        public virtual bool CanSort => PageParams.SortTypes?.Any() == true;
        public virtual bool CanFilterByYear => PageParams.YearLimit.HasValue;
        public virtual bool CanFilterByTags => PageParams.TagsContainers?.Any() == true;

        public bool CanAnyInteract => CanSort || CanFilterByTags || CanFilterByYear;

        public IIncrementalCollection<ItemsListItemViewModel> ItemsSource
        {
            get => Get(IncrementalLoadingCollection.Empty<ItemsListItemViewModel>());
            protected set => Set(value);
        }

        private void SelectedTags_CollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.Action == NotifyCollectionChangedAction.Add
                || args.Action == NotifyCollectionChangedAction.Remove
                || args.Action == NotifyCollectionChangedAction.Reset)
            {
                IsRefreshRequired = true;
            }
        }

        private void SectionPageFilter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Year)
                || e.PropertyName == nameof(FilterByFavorites)
                || e.PropertyName == nameof(FilterByInHistory)
                || e.PropertyName == nameof(CurrentSortType))
            {
                IsRefreshRequired = true;
            }
        }
    }
}

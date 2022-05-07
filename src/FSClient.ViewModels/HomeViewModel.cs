namespace FSClient.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;
    using FSClient.ViewModels.Abstract;
    using FSClient.ViewModels.Pages;

    public class HomeViewModel : ViewModelBase
    {
        private bool needReinit;

        private readonly IItemManager itemManager;
        private readonly IProviderManager providerManager;
        private readonly IContentDialog<string, bool> confirmDialog;
        private readonly IIncrementalCollectionFactory collectionFactory;
        private readonly INotificationService notificationService;

        public HomeViewModel(
            IItemManager itemManager,
            IProviderManager providerManager,
            IIncrementalCollectionFactory collectionFactory,
            IContentDialog<string, bool> confirmDialog,
            INotificationService notificationService)
        {
            this.itemManager = itemManager;
            this.providerManager = providerManager;
            this.confirmDialog = confirmDialog;
            this.collectionFactory = collectionFactory;
            this.notificationService = notificationService;

            needReinit = true;

            ShortSearchCommand = new AsyncCommand(
                UpdateSearchSuggest,
                AsyncCommandConflictBehaviour.CancelPrevious);

            UpdateSourceCommand = new AsyncCommand(
                UpdateSourceAsync,
                AsyncCommandConflictBehaviour.Skip);

            Settings.PropertyChanged += async (s, a) =>
            {
                if (a.PropertyName == nameof(Settings.MainSite))
                {
                    needReinit = true;
                    SearchSuggest = null;
                    await UpdateSourceCommand.ExecuteAsync(default).ConfigureAwait(false);
                }
            };
        }

        public IReadOnlyCollection<PageViewModel> Pages
        {
            get => Get<IReadOnlyCollection<PageViewModel>>(new List<PageViewModel>());
            private set => Set(value);
        }

        public HomePageViewModel? CurrentHomePage => CurrentPage as HomePageViewModel;

        public SectionPageViewModel? CurrentSectionPage => CurrentPage as SectionPageViewModel;

        public PageViewModel? CurrentPage
        {
            get => Get<PageViewModel>();
            set
            {
                if (Set(value))
                {
                    OnPropertyChanged(nameof(CurrentHomePage), nameof(CurrentSectionPage));
                }
            }
        }

        public string SearchRequest
        {
            get => Get(string.Empty);
            set => Set(value ?? string.Empty);
        }

        public IReadOnlyCollection<ItemInfo>? SearchSuggest
        {
            get => Get<IReadOnlyCollection<ItemInfo>>(new List<ItemInfo>());
            set => Set(value);
        }

        public AsyncCommand UpdateSourceCommand { get; }

        public AsyncCommand ShortSearchCommand { get; }

        private async Task UpdateSourceAsync(CancellationToken cancellationToken)
        {
            if (needReinit)
            {
                needReinit = false;

                var pages = await itemManager
                    .GetSectionPageFiltersAsync(Settings.MainSite, cancellationToken)
                    .ToFlatAsyncEnumerable()
                    .Where(page => page?.PageParams.Section != Section.Any)
                    .SelectAwait(async filter =>
                    {
                        var page = (PageViewModel)new SectionPageViewModel(itemManager, collectionFactory, filter);
                        await page.UpdateCommand.ExecuteAsync(true, cancellationToken).ConfigureAwait(false);
                        return page;
                    })
                    .ToListAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (itemManager.HasProviderHomePage(Settings.MainSite))
                {
                    var homePageViewModel = new HomePageViewModel(
                        itemManager, providerManager, confirmDialog, notificationService, Settings.MainSite);
                    pages.Insert(0, homePageViewModel);
                    Pages = pages;

                    await homePageViewModel.UpdateCommand.ExecuteAsync(true, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Pages = pages;
                }
            }
        }

        private async Task UpdateSearchSuggest(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(SearchRequest))
            {
                return;
            }

            try
            {
                SearchSuggest = await itemManager
                    .GetShortSearchResult(
                        SearchRequest,
                        Settings.MainSite,
                        CurrentSectionPage?.PageParams.Section ?? Section.Any)
                    .Take(10)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancelation
            }
        }
    }
}

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
    using FSClient.Shared.Services;
    using FSClient.ViewModels.Items;
    using FSClient.ViewModels.Pages;

    public class SearchViewModel : ViewModelBase, IStateSaveable
    {
        private readonly IItemManager itemManager;
        private readonly IProviderManager providerManager;
        private readonly IUserManager userManager;
        private readonly IProviderConfigService providerConfigService;
        private readonly INotificationService notificationService;
        private readonly IIncrementalCollectionFactory collectionFactory;
        private bool needToUpdateSource = true;

        public SearchViewModel(
            IItemManager itemManager,
            IProviderManager providerManager,
            IUserManager userManager,
            IProviderConfigService providerConfigService,
            INotificationService notificationService,
            IIncrementalCollectionFactory collectionFactory)
        {
            this.itemManager = itemManager;
            this.providerManager = providerManager;
            this.userManager = userManager;
            this.providerConfigService = providerConfigService;
            this.notificationService = notificationService;
            this.collectionFactory = collectionFactory;
            UpdateProviders(true);

            UpdateSourceCommand = new AsyncCommand(UpdateSourceAsync, AsyncCommandConflictBehaviour.Skip);

            SearchCommand = new AsyncCommand<bool>(
                SearchAsync,
                _ => !string.IsNullOrWhiteSpace(SearchRequest)
                    && ResultPages.All(p => SearchRequest.Length >= p.PageParams.MinimumRequestLength),
                AsyncCommandConflictBehaviour.CancelPrevious);

            SetProviderCommand = new Command<Site>(site =>
                CurrentProvider = SearchProviders.FirstOrDefault(p => p.Site == (site == default ? Settings.MainSite : site)));

            Settings.PropertyChanged += async (s, a) =>
            {
                if (a.PropertyName == nameof(Settings.MainSite))
                {
                    await UpdateSourceCommand.ExecuteAsync(default).ConfigureAwait(false);
                    var wasMainSite = SearchProviders.FirstOrDefault() == CurrentProvider;
                    UpdateProviders(wasMainSite);
                }
            };
        }

        public Command<Site> SetProviderCommand { get; set; }

        public AsyncCommand<bool> SearchCommand { get; }

        public AsyncCommand UpdateSourceCommand { get; }

        public IReadOnlyCollection<ProviderViewModel> SearchProviders
        {
            get => Get<IReadOnlyCollection<ProviderViewModel>>(new List<ProviderViewModel>());
            private set => Set(value);
        }

        public SearchPageViewModel? CurrentPage
        {
            get => Get<SearchPageViewModel>();
            set => Set(value);
        }

        public IReadOnlyCollection<SearchPageViewModel> ResultPages
        {
            get => Get<IReadOnlyCollection<SearchPageViewModel>>(new List<SearchPageViewModel>());
            private set => Set(value);
        }

        public ProviderViewModel? CurrentProvider
        {
            get => Get<ProviderViewModel>();
            set
            {
                if (value != null
                    && Set(value))
                {
                    needToUpdateSource = true;
                }
            }
        }

        public string SearchRequest
        {
            get => Get(string.Empty);
            set
            {
                value ??= string.Empty;
                if (Set(value))
                {
                    foreach (var page in ResultPages)
                    {
                        page.SearchRequest = value;
                    }
                }
            }
        }

        public Uri? SaveStateToUri()
        {
            return UriParserHelper.GetProtocolUriFromViewModel(NavigationPageType.Search,
                new Dictionary<string, string?>
                {
                    ["request"] = SearchRequest,
                    ["site"] = CurrentProvider?.Site.Value
                });
        }

        private async Task SearchAsync(bool force, CancellationToken cancellationToken)
        {
            if (Uri.TryCreate(SearchRequest, UriKind.Absolute, out var possibleItemInfoLink))
            {
                var singleItem = await itemManager.OpenFromLinkAsync(possibleItemInfoLink, cancellationToken).ConfigureAwait(false);
                if (singleItem != null)
                {
                    var singlePage = new SingleItemSearchPageViewModel(singleItem, itemManager, collectionFactory);
                    ResultPages = new[] { singlePage };
                    needToUpdateSource = true;
                    return;
                }
            }

            if (needToUpdateSource)
            {
                await UpdateSourceCommand.ExecuteAsync(default).ConfigureAwait(false);
            }
            await ResultPages
                .Where(p => SearchRequest.Length >= p.PageParams.MinimumRequestLength)
                .ToAsyncEnumerable()
                .WhenAllAsync((p, token) => p.UpdateCommand.ExecuteAsync(force, token), cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task UpdateSourceAsync(CancellationToken cancellationToken)
        {
            var site = CurrentProvider?.Site ?? Settings.MainSite;
            ResultPages = await itemManager
                .GetSearchPageFiltersAsync(site, cancellationToken)
                .ToFlatAsyncEnumerable()
                .Select(page => new SearchPageViewModel(itemManager, collectionFactory, page)
                {
                    SearchRequest = SearchRequest
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            needToUpdateSource = false;
        }

        private void UpdateProviders(bool replaceCurrent)
        {
            SearchProviders = itemManager
                .GetSearchProviders()
                .OrderByDescending(p => p.CanBeMain)
                .ThenByDescending(p => p.Site == Settings.MainSite)
                .Select(p => new ProviderViewModel(providerManager, providerConfigService, userManager, p, notificationService))
                .ToList();
            if (replaceCurrent)
            {
                CurrentProvider = SearchProviders.FirstOrDefault();
            }
        }
    }
}

namespace FSClient.ViewModels.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.ViewModels.Abstract;
    using FSClient.ViewModels.Items;

    public class HomePageViewModel : PageViewModel
    {
        private static readonly List<Site> brokenSites = new List<Site>();
        private string caption = Strings.HomePageModel_Caption;

        private readonly IItemManager itemManager;
        private readonly IProviderManager providerManager;
        private readonly IContentDialog<string, bool> confirmDialog;
        private readonly INotificationService notificationService;
        private readonly Site site;

        public HomePageViewModel(
            IItemManager itemManager,
            IProviderManager providerManager,
            IContentDialog<string, bool> confirmDialog,
            INotificationService notificationService,
            Site site)
        {
            this.itemManager = itemManager;
            this.providerManager = providerManager;
            this.confirmDialog = confirmDialog;
            this.notificationService = notificationService;
            this.site = site;
        }

        public string? TopItemsCaption
        {
            get => Get<string>();
            private set => Set(value);
        }

        public IReadOnlyCollection<ItemsListItemViewModel> TopItems
        {
            get => Get<IReadOnlyCollection<ItemsListItemViewModel>>(new List<ItemsListItemViewModel>());
            private set => Set(value);
        }

        public IReadOnlyCollection<IGrouping<string, ItemsListItemViewModel>> HomeItems
        {
            get => Get<IReadOnlyCollection<IGrouping<string, ItemsListItemViewModel>>>(new List<IGrouping<string, ItemsListItemViewModel>>());
            private set => Set(value);
        }

        public override string Caption => caption;

        protected override async Task UpdateAsync(bool force, CancellationToken cancellationToken)
        {
            var result = false;
            using (var localCts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(localCts.Token, cancellationToken))
            {
                var token = linkedCts.Token;
                var repeatCount = 0;
                while (!result
                    && repeatCount++ < 2)
                {
                    ShowProgress = true;
                    var page = await itemManager.GetHomePageModelAsync(site, token).ConfigureAwait(false);
                    result = page != null;
                    if (page != null)
                    {
                        caption = page.Caption;
                        TopItemsCaption = page.TopItemsCaption;
                        TopItems = page.TopItems.Select(i => new ItemsListItemViewModel(i, Shared.Providers.DisplayItemMode.Normal, itemManager)).ToList();
                        HomeItems = page.HomeItems
                            .SelectMany(group => group.Select(i => (group.Key, Item: new ItemsListItemViewModel(i, Shared.Providers.DisplayItemMode.Normal, itemManager))))
                            .GroupBy(t => t.Key, t => t.Item)
                            .ToList();
                        OnPropertyChanged(nameof(Caption));
                    }
                    ShowProgress = false;

                    if (!result)
                    {
                        await notificationService
                            .ShowAsync(
                                string.Format(Strings.HomePageViewModel_SiteIsNotAvailableNow, site.Title),
                                NotificationType.Warning)
                            .ConfigureAwait(false);
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            if (!result
                && !brokenSites.Contains(Settings.MainSite))
            {
                brokenSites.Add(Settings.MainSite);
                var nextSite = providerManager.GetMainProviders().FirstOrDefault(s => !brokenSites.Contains(s));

                if (nextSite != Site.Any)
                {
                    var dialogResult = await confirmDialog
                        .ShowAsync(
                            string.Format(Strings.HomePageViewModel_UnableConnectToSiteAndAskForAnotherSite, Settings.MainSite, nextSite),
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (dialogResult)
                    {
                        Settings.MainSite = nextSite;
                    }
                }
            }
        }
    }
}

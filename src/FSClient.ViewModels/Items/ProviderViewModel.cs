namespace FSClient.ViewModels.Items
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class ProviderViewModel : ViewModelBase
    {
        private Uri? currentMirror;
        private AuthUserViewModel? authUserViewModel;

        private readonly ISiteProvider siteProvider;
        private readonly IProviderManager providerManager;
        private readonly IUserManager userManager;
        private readonly IProviderConfigService providerConfigService;
        private readonly INotificationService notificationService;

        public ProviderViewModel(
            IProviderManager providerManager,
            IProviderConfigService providerConfigService,
            IUserManager userManager,
            ISiteProvider siteProvider,
            INotificationService notificationService)
        {
            this.siteProvider = siteProvider;
            this.providerManager = providerManager;
            this.userManager = userManager;
            this.providerConfigService = providerConfigService;
            this.notificationService = notificationService;
            HasAuthProvider = userManager.GetAuthModels(siteProvider.Site).Any();
        }

        public ProviderDetails Details => siteProvider.Details;

        public AuthUserViewModel? AuthUserViewModel => GetAuthUserViewModel();

        public bool HasAuthProvider { get; }

        public Site Site => siteProvider.Site;
        public string Name => siteProvider.Site.Title;
        public IReadOnlyList<Uri> Mirrors => siteProvider.Mirrors;

        public Uri? CurrentMirror => GetCurrentMirror();

        public bool CanBeMain => siteProvider.CanBeMain;

        public Uri? UserMirror
        {
            get => providerConfigService.GetUserMirror(Site);
            set => providerConfigService.SetUserMirror(Site, value);
        }

        public bool EnforceDisabled => siteProvider.EnforceDisabled;

        public bool IsEnabled
        {
            get => providerManager.IsProviderEnabled(Site);
            set => providerConfigService.SetIsEnabledByUser(Site, value);
        }

        public string RequirementsText => Details.Requirement switch
        {
            ProviderRequirements.AccountForAny => Strings.ProviderRequirements_AccountForAny,
            ProviderRequirements.AccountForSpecial => Strings.ProviderRequirements_AccountForSpecial,
            ProviderRequirements.ProForAny => Strings.ProviderRequirements_ProForAny,
            ProviderRequirements.ProForSpecial => Strings.ProviderRequirements_ProForSpecial,
            _ => string.Empty
        };

        private Uri? GetCurrentMirror()
        {
            if (currentMirror != null)
            {
                return currentMirror;
            }
            var mirrrorTask = siteProvider.GetMirrorAsync(default);
            if (mirrrorTask.IsCompleted)
            {
                return currentMirror = mirrrorTask.Result;
            }

            ContinueWith(mirrrorTask, this);

            return null;

            static async void ContinueWith(ValueTask<Uri> task, ProviderViewModel providerModel)
            {
                providerModel.currentMirror = await task.ConfigureAwait(true);
                providerModel.OnPropertyChanged(nameof(CurrentMirror));
            }
        }

        private AuthUserViewModel? GetAuthUserViewModel()
        {
            if (authUserViewModel != null
                || !HasAuthProvider)
            {
                return authUserViewModel;
            }

            authUserViewModel = new AuthUserViewModel(userManager, notificationService);
            authUserViewModel.SetSiteCommand.Execute(Site);
            OnPropertyChanged(nameof(CurrentMirror));

            return authUserViewModel;
        }
    }
}

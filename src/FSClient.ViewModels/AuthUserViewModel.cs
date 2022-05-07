namespace FSClient.ViewModels
{
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

    public class AuthUserViewModel : ViewModelBase
    {
        private readonly IUserManager userManager;
        private readonly INotificationService notificationService;

        public AuthUserViewModel(IUserManager userManager, INotificationService notificationService)
        {
            this.userManager = userManager;
            this.notificationService = notificationService;

            userManager.UserLoggedOut += site =>
            {
                if (site == Site)
                {
                    User = null;
                    OnPropertyChanged(nameof(User));
                }
            };
            userManager.UserLoggedIn += (site, newUser) =>
            {
                if (site == Site)
                {
                    User = newUser;
                    OnPropertyChanged(nameof(User));
                }
            };

            SetSiteCommand = new AsyncCommand<Site>(
                SetSiteAsync,
                AsyncCommandConflictBehaviour.CancelPrevious);

            RegisterCommand = new AsyncCommand(
                ct => userManager.RegisterAsync(Site, ct),
                () => Site != default,
                AsyncCommandConflictBehaviour.Skip);

            LoginCommand = new AsyncCommand<AuthModel>(
                LoginAsync,
                _ => Site != default,
                AsyncCommandConflictBehaviour.CancelPrevious);

            LogoutCommand = new AsyncCommand(
                ct => userManager.LogoutAsync(Site, ct),
                () => User != null && Site != default,
                AsyncCommandConflictBehaviour.Skip);
        }

        public Site Site { get; private set; }

        public User? User { get; private set; }

        public IEnumerable<AuthModel> AuthModels { get; private set; } = Enumerable.Empty<AuthModel>();

        public AsyncCommand<Site> SetSiteCommand { get; }
        public AsyncCommand RegisterCommand { get; }
        public AsyncCommand<AuthModel> LoginCommand { get; }
        public AsyncCommand LogoutCommand { get; }

        private async Task SetSiteAsync(Site site, CancellationToken cancellationToken)
        {
            Site = site;

            User = await userManager.GetCurrentUserAsync(site, default).ConfigureAwait(true);

            AuthModels = userManager.GetAuthModels(site);

            OnPropertyChanged(nameof(Site), nameof(User), nameof(AuthModels));

            LogoutCommand.RaiseCanExecuteChanged();
            RegisterCommand.RaiseCanExecuteChanged();
            LoginCommand.RaiseCanExecuteChanged();
        }

        private async Task LoginAsync(AuthModel model, CancellationToken cancellationToken)
        {
            var authModel = model ?? AuthModels.FirstOrDefault();
            if (authModel == null)
            {
                return;
            }

            var (user, status) = await userManager.AuthorizeAsync(Site, authModel, cancellationToken).ConfigureAwait(true);
            User = user;

            if (status.GetDisplayDescription() is string message)
            {
                var notificationType = status switch
                {
                    AuthStatus.Success => NotificationType.Completed,
                    AuthStatus.Canceled => NotificationType.Information,
                    AuthStatus.Error => NotificationType.Error,
                    _ => NotificationType.Warning
                };
                await notificationService
                    .ShowAsync(
                        message,
                        notificationType)
                    .ConfigureAwait(false);
            }
        }
    }
}

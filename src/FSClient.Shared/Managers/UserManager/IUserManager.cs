namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public interface IUserManager
    {
        event Action<Site, User> UserLoggedIn;

        event Action<Site> UserLoggedOut;

        ValueTask<User?> GetCurrentUserAsync(Site site, CancellationToken cancellationToken);

        ValueTask<bool> CheckRequirementsAsync(Site site, ProviderRequirements providerRequirements, CancellationToken cancellationToken);

        Task<(User? user, AuthStatus status)> AuthorizeAsync(Site site, AuthModel authModel, CancellationToken cancellationToken);

        IEnumerable<AuthModel> GetAuthModels(Site site);

        Task LogoutAsync(Site site, CancellationToken cancellationToken);

        Task<bool> RegisterAsync(Site site, CancellationToken cancellationToken);
    }
}

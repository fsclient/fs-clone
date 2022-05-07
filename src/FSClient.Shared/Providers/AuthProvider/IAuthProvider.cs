namespace FSClient.Shared.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAuthProvider : IProvider
    {
        IEnumerable<AuthModel> AuthModels { get; }

        ValueTask<Uri> GetRegisterLinkAsync(CancellationToken cancellationToken);

        ValueTask<AuthResult> TryGetOrRefreshUserAsync(IEnumerable<Cookie> cookies, CancellationToken cancellationToken);

        Task LogoutAsync(CancellationToken cancellationToken);

        Task<AuthResult> AuthorizeAsync(AuthModel authModel, CancellationToken cancellationToken);
    }
}

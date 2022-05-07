namespace FSClient.Shared.Providers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    public class AuthResult
    {
        public AuthResult(AuthStatus authStatus)
        {
            AuthStatus = authStatus;
            Cookies = Enumerable.Empty<Cookie>();
        }

        public AuthResult(AuthStatus authStatus, User user, IEnumerable<Cookie> cookies)
        {
            AuthStatus = authStatus;
            User = user;
            Cookies = cookies;
        }

        public AuthStatus AuthStatus { get; }

        public User? User { get; }

        public IEnumerable<Cookie> Cookies { get; }
    }
}

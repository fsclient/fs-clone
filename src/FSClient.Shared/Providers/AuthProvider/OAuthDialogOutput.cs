namespace FSClient.Shared.Providers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    public class OAuthDialogOutput
    {
        public OAuthDialogOutput(
            AuthStatus status,
            string? data,
            IEnumerable<Cookie> cookies)
        {
            Status = status;
            Data = data;
            Cookies = cookies;
        }


        public OAuthDialogOutput(
            AuthStatus status,
            string? data)
            : this(status, data, Enumerable.Empty<Cookie>())
        {
        }

        public OAuthDialogOutput(
            AuthStatus status)
            : this(status, null, Enumerable.Empty<Cookie>())
        {
        }

        public string? Data { get; }
        public AuthStatus Status { get; }
        public IEnumerable<Cookie> Cookies { get; }
    }
}

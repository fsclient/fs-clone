namespace FSClient.Shared.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class OAuthDialogInput
    {
        public OAuthDialogInput(
            Uri startLink,
            Uri endLink,
            IEnumerable<string> requestCookies)
        {
            StartLink = startLink;
            EndLink = endLink;
            RequestCookies = requestCookies;
        }

        public OAuthDialogInput(
            Uri startLink,
            Uri endLink)
            : this(startLink, endLink, Enumerable.Empty<string>())
        {
        }

        public Uri StartLink { get; }

        public Uri EndLink { get; }

        public IEnumerable<string> RequestCookies { get; }
    }
}

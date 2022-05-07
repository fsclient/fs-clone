namespace FSClient.Shared.Providers
{
    using System;

    public class AuthModel
    {
        public Uri? AuthLink { get; }
        public bool IsOAuth { get; set; }
        public string? Title { get; set; }
        public Uri? Icon { get; set; }

        public AuthModel()
        {

        }

        public AuthModel(Uri link)
        {
            AuthLink = link;
        }
    }
}

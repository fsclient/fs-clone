namespace FSClient.Providers.Test.Stubs
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;

    public class PlayerParseManagerStub : IPlayerParseManager
    {
        public bool CanOpenFromLinkOrHostingName(Uri httpUri, Site knownHosting)
        {
            return true;
        }

        public Task<File?> ParseFromUriAsync(Uri httpUri, Site knownHosting, CancellationToken cancellationToken)
        {
            var file = new File(knownHosting, "notempty");
            file.Title = "Fake title";
            return Task.FromResult<File?>(file);
        }
    }
}

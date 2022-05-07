namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class TMDbFileProvider : IFileProvider
    {
        private readonly TMDbSiteProvider siteProvider;
        private readonly IPlayerParseManager playerParseManager;

        public TMDbFileProvider(
            TMDbSiteProvider siteProvider,
            IPlayerParseManager playerParseManager)
        {
            this.siteProvider = siteProvider;
            this.playerParseManager = playerParseManager;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => false;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
        }

        public async Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (itemInfo == null)
            {
                return null;
            }

            var section = itemInfo.Section.Modifier.HasFlag(SectionModifiers.Serial) ? "tv" : "movie";
            var json = await siteProvider
                .GetFromApiAsync(
                    $"{section}/{itemInfo.SiteId}/videos",
                    cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            var files = await (json?["results"] as JArray ?? new JArray())
                .OfType<JObject>()
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation(async (i, ct) =>
                {
                    if (i["site"]?.ToString() != "YouTube"
                        || i["id"]?.ToString() is not string id
                        || i["key"]?.ToString() is not string key)
                    {
                        return null;
                    }
                    var link = new Uri("https://www.youtube.com/watch?v=" + key);
                    if (!playerParseManager.CanOpenFromLinkOrHostingName(link, Sites.Youtube))
                    {
                        return null;
                    }

                    var file = await playerParseManager.ParseFromUriAsync(link, Sites.Youtube, ct).ConfigureAwait(false);
                    if (file == null)
                    {
                        return null;
                    }

                    file.IsTrailer = true;
                    file.Title ??= itemInfo.Title;

                    return file;
                })
                .Where(f => f != null)!
                .OfType<File>()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)!;

            var folder = new Folder(Site, $"tmdb_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
            folder.AddRange(files);
            return folder;
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            throw new NotSupportedException();
        }
    }
}

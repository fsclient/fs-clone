namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class ShikiFileProvider : IFileProvider
    {
        private readonly ShikiSiteProvider siteProvider;
        private readonly SmotretAnimeFileProvider smotretAnimeFileProvider;
        private readonly IPlayerParseManager playerParseManager;

        public ShikiFileProvider(
            ShikiSiteProvider siteProvider,
            SmotretAnimeFileProvider smotretAnimeFileProvider,
            IPlayerParseManager playerParseManager)
        {
            this.siteProvider = siteProvider;
            this.smotretAnimeFileProvider = smotretAnimeFileProvider;
            this.playerParseManager = playerParseManager;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => false;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            smotretAnimeFileProvider.InitForItems(items);
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            return await smotretAnimeFileProvider.GetFolderChildrenAsync(folder, token).ConfigureAwait(false);
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(itemInfo?.SiteId))
            {
                return null;
            }

            var json = await siteProvider
                .GetFromApiAsync($"api/animes/{itemInfo!.SiteId}/videos", cancellationToken)
                .AsNewtonsoftJson<JArray>()
                .ConfigureAwait(false);
            if (json == null)
            {
                return null;
            }

            var files = json
                .OfType<JObject>()
                .Select(t =>
                {
                    Uri.TryCreate(t["image_url"]?.ToString(), UriKind.Absolute, out var poster);

                    if (Uri.TryCreate(t["url"]?.ToString(), UriKind.Absolute, out var link))
                    {
                        var file = new File(Site, $"s{itemInfo.SiteId}_{t["id"]}_t")
                        {
                            Title = t["name"]?.ToString().NotEmptyOrNull()
                                ?? GetFriendlyVideoKindName(t["kind"]?.ToString()),
                            IsTrailer = true,
                            PlaceholderImage = poster
                        };
                        if (!(t["hosting"]?.ToString() is string hosting
                            && Site.Parse(hosting, Site.Any, true) is var hostingSite)
                            || !playerParseManager.CanOpenFromLinkOrHostingName(link, hostingSite))
                        {
                            return null;
                        }

                        file.SetVideosFactory(async (_, cancellationToken) =>
                        {
                            var file = await playerParseManager
                                .ParseFromUriAsync(link, hostingSite, cancellationToken)
                                .ConfigureAwait(false);
                            return file?.Videos ?? Enumerable.Empty<Video>();
                        });

                        return file;
                    }
                    return null;
                })
                .Where(f => f != null)!;

            var folder = new Folder(Site, $"shk_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
            folder.AddRange(files!);
            return folder;
        }

        private static string? GetFriendlyVideoKindName(string? kind)
        {
            return (kind?.ToLowerInvariant()) switch
            {
                "pv" => "Promotional Video",
                "ed" => "Ending",
                "op" => "Opening",
                _ => kind,
            };
        }
    }
}

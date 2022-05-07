namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class ZonaFileProvider : IFileProvider
    {
        private readonly ZonaSiteProvider siteProvider;

        public ZonaFileProvider(
            ZonaSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        private ItemInfo? currentItem;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItem = items
                .FirstOrDefault();
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (folder is ZonaFolder zonaFolder)
            {
                return await GetSeasonAsync(zonaFolder.NamedId, zonaFolder.Season ?? 1, token).ConfigureAwait(false);
            }
            else
            {
                if (currentItem?.Link != null
                    && currentItem.Section.Modifier.HasFlag(SectionModifiers.Serial))
                {
                    return await ParseSerialAsync(currentItem.Link, token).ConfigureAwait(false);
                }
                else if (currentItem?.SiteId?.ToIntOrNull() is int id)
                {
                    return await ParseFilmAsync(currentItem.Link, currentItem.Title ?? currentItem.Details.TitleOrigin, id, token).ConfigureAwait(false);
                }
            }

            return Enumerable.Empty<ITreeNode>();
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        private async Task<IEnumerable<ITreeNode>> ParseFilmAsync(Uri? itemLink, string? title, int id, CancellationToken token)
        {
            var domain = await siteProvider.GetMirrorAsync(token).ConfigureAwait(false);

            var file = new File(Site, "z" + id)
            {
                Title = title,
                FrameLink = new Uri(domain, itemLink)
            };
            file.SetVideosFactory((_, cancellationToken) => GetVideosAsync(id, cancellationToken));
            return new[] { file };
        }

        private async Task<IEnumerable<ITreeNode>> ParseSerialAsync(Uri itemLink, CancellationToken token)
        {
            var nameId = itemLink.GetPath().Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
            if (nameId == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var domain = await siteProvider.GetMirrorAsync(token).ConfigureAwait(false);

            if (!Uri.TryCreate(domain, $"/api/v1/tvseries/{nameId}", out var apiLink))
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var json = await siteProvider
                .HttpClient
                .GetBuilder(apiLink)
                .WithAjax()
                .SendAsync(token)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            if (json == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var count = json["seasons_count"]?.ToIntOrNull() ?? json["seasons"]?["count"]?.ToIntOrNull() ?? 0;
            var id = json["id"]?.ToIntOrNull() ?? json["serial"]?["id"]?.ToIntOrNull();

            if (count == 0)
            {
                var curId = json["mobi_link_id"]?.ToIntOrNull() ?? json["episodes"]?["current_mobi_link_id"]?.ToIntOrNull();

                if (!curId.HasValue)
                {
                    return Enumerable.Empty<ITreeNode>();
                }

                var file = new File(Site, $"z{id}")
                {
                    FrameLink = new Uri(domain, itemLink),
                    Title = json["name_rus"]?.ToString() ?? json["serial"]?["name_rus"]?.ToString()
                };
                file.SetVideosFactory((_, cancellationToken) => GetVideosAsync(curId.Value, cancellationToken));
                return new[] { file };
            }

            return Enumerable.Range(1, count)
                .Select(num => new ZonaFolder(Site, $"z{id}_{num}", nameId, FolderType.Season, PositionBehavior.Average)
                {
                    Season = num,
                    Title = "Сезон " + num
                });
        }

        private async Task<IEnumerable<ITreeNode>> GetSeasonAsync(string nameId, int seasonNumber, CancellationToken token)
        {
            var domain = await siteProvider.GetMirrorAsync(token).ConfigureAwait(false);

            var link = new Uri(domain, $"/tvseries/{nameId}/season-{seasonNumber}");

            var json = await siteProvider
                .HttpClient
                .GetBuilder(link)
                .WithAjax()
                .SendAsync(token)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            if (json?["episodes"]?["items"] is not JObject items
                || items.Count == 0)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            return items.Properties()
                .Select(jProperty =>
                {
                    var item = jProperty.Value;

                    var id = item["mobi_link_id"]?.ToIntOrNull();
                    if (!id.HasValue)
                    {
                        return null;
                    }

                    var file = new File(Site, "z" + id)
                    {
                        FrameLink = link,
                        Title = item["title"]?.ToString(),
                        Season = seasonNumber,
                        Episode = item["episode"]?.ToIntOrNull().ToRange()
                    };
                    file.SetVideosFactory((_, cancellationToken) => GetVideosAsync(id.Value, cancellationToken));

                    return file;
                })
                .Where(f => f != null)!;
        }

        private async Task<IEnumerable<Video>> GetVideosAsync(int id, CancellationToken cancellationToken)
        {
            var videos = new List<Video>();

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var androidJson = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, $"/api/v1/video/{id}"))
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            if (Uri.TryCreate(androidJson?["url"]?.ToString(), UriKind.Absolute, out var hdUrl))
            {
                videos.Add(new Video(hdUrl)
                {
                    Quality = 1080,
                    LowPriority = true
                });
            }

            if (Uri.TryCreate(androidJson?["lqUrl"]?.ToString(), UriKind.Absolute, out var sdUrl))
            {
                videos.Add(new Video(sdUrl)
                {
                    Quality = 360
                });
            }

            if (videos.Count == 0)
            {
                var json = await siteProvider
                    .HttpClient
                    .GetBuilder(new Uri(domain, $"/ajax/video/{id}"))
                    .WithAjax()
                    .SendAsync(cancellationToken)
                    .AsNewtonsoftJson<JObject>()
                    .ConfigureAwait(false);

                if (Uri.TryCreate(json?["url"]?.ToString(), UriKind.Absolute, out var url))
                {
                    videos.Add(new Video(url)
                    {
                        Quality = 360
                    });
                }
            }

            return videos;
        }

        public class ZonaFolder : Folder
        {
            public string NamedId { get; }

            public ZonaFolder(Site site, string id, string namedId, FolderType folderType, PositionBehavior positionBehavior)
                : base(site, id, folderType, positionBehavior)
            {
                NamedId = namedId;
            }
        }
    }
}

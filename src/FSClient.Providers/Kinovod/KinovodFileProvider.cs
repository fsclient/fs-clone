namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class KinovodFileProvider : IFileProvider
    {
        private static readonly Regex indentifiesParserRegex = new Regex(
            @"(?:VOD_TIME\s*=\s*""(?<VOD_TIME>.+?)"")|(?:VOD_HASH\s*=\s*""(?<VOD_HASH>.+?)"")|(?:IDENTIFIER\s*=\s*""(?<IDENTIFIER>.+?)"")",
            RegexOptions.Compiled);

        private readonly KinovodSiteProvider siteProvider;
        private readonly PlayerJsParserService playerJsParserService;
        private readonly IPlayerParseManager playerParseManager;

        public KinovodFileProvider(
            KinovodSiteProvider siteProvider,
            PlayerJsParserService playerJsParserService,
            IPlayerParseManager playerParseManager)
        {
            this.siteProvider = siteProvider;
            this.playerJsParserService = playerJsParserService;
            this.playerParseManager = playerParseManager;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        private ItemInfo? currentItem;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItem = items
                .FirstOrDefault();
        }

        public Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken cancellationToken)
        {
            if (folder == null)
            {
                return Task.FromException<IEnumerable<ITreeNode>>(new ArgumentNullException(nameof(folder)));
            }
            if (folder.ItemsSource.Count > 0)
            {
                return Task.FromResult(Enumerable.Empty<ITreeNode>());
            }

            return ParseNodesFromPageAsync(currentItem!.Link!, currentItem.Title, currentItem.SiteId!, cancellationToken);
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var pageLink = new Uri(domain, itemInfo.Link);

            var pageText = await siteProvider
                .HttpClient
                .GetBuilder(pageLink)
                .WithHeader("Refrerer", domain.ToString())
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            var youtubeLink = Regex.Match(pageText ?? string.Empty, @"^.+?'#trailer'.+?iframe.+?(?<link>http.+?youtube[^""]*)""", RegexOptions.Multiline).Groups["link"].Value.ToUriOrNull();
            if (youtubeLink == null
                || !playerParseManager.CanOpenFromLinkOrHostingName(youtubeLink, Sites.Youtube))
            {
                return null;
            }

            var file = await playerParseManager.ParseFromUriAsync(youtubeLink, Sites.Youtube, cancellationToken).ConfigureAwait(false);
            if (file != null)
            {
                file.IsTrailer = true;
            }

            var folder = new Folder(Site, $"knv_t_{itemInfo.SiteId}", FolderType.Item, PositionBehavior.Average);
            folder.Add(file!);
            return folder;
        }

        private async Task<IEnumerable<ITreeNode>> ParseNodesFromPageAsync(Uri itemLink, string? title, string siteId, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var pageLink = new Uri(domain, itemLink);

            var pageText = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, itemLink))
                .WithHeader("Refrerer", domain.ToString())
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            var matches = indentifiesParserRegex.Matches(pageText ?? string.Empty).Cast<Match>();
            var vodTime = matches.Select(m => m.Groups["VOD_TIME"].Value).FirstOrDefault(v => !string.IsNullOrEmpty(v));
            var vodHash = matches.Select(m => m.Groups["VOD_HASH"].Value).FirstOrDefault(v => !string.IsNullOrEmpty(v));
            var identifier = matches.Select(m => m.Groups["IDENTIFIER"].Value).FirstOrDefault(v => !string.IsNullOrEmpty(v));

            if (vodTime == null
                || vodHash == null
                || identifier == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var translation = Regex.Match(pageText, @"<li><b>Перевод:<\/b>(?<translation>[^<]+)<\/li>").Groups["translation"].Value.NotEmptyOrNull();

            var itemInfoText = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, $"/vod/{siteId}"))
                .WithArguments(new Dictionary<string, string?>
                {
                    ["player_type"] = "new",
                    ["file_type"] = "mp4",
                    ["identifier"] = identifier,
                    ["st"] = vodHash,
                    ["e"] = vodTime
                })
                .WithAjax()
                .WithHeader("Refrerer", domain.ToString())
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);
            
            var playlistInfoText = await playerJsParserService
                .DecodeAsync(
                    itemInfoText?.Split('|').Skip(1).Take(1).FirstOrDefault(),
                    siteProvider.PlayerJsConfig,
                    cancellationToken)
                .ConfigureAwait(false);
            if (playlistInfoText == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            return ProviderHelper.ParsePlaylistFromPlayerJsString(
                siteProvider.HttpClient, Site, $"knv{siteId}", playlistInfoText, translation, title, pageLink, LocalizationHelper.RuLang);
        }
    }
}

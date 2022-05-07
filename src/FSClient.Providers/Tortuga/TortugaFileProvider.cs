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
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class TortugaFileProvider : IFileProvider
    {
        private static readonly Regex fileRegex = new Regex(@"file\s*:\s*""(?<file>.*?)""");
        private static readonly Regex serialRegex = new Regex(@"file\s*:\s*'(?<file>.*?)'");

        private readonly TortugaSiteProvider siteProvider;

        public TortugaFileProvider(TortugaSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => false;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {

        }

        public Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task<IEnumerable<ITreeNode>> GetVideosFromTortugaAsync(
            Uri tortugaLink,
            Uri referer,
            string? baseId,
            string? title,
            string? translationTitle,
            CancellationToken cancellationToken)
        {
            var tortugaId = tortugaLink.Segments.Skip(2).FirstOrDefault()?.GetDigits().ToIntOrNull();
            if (tortugaId == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            var text = await siteProvider
                .HttpClient
                .GetBuilder(tortugaLink)
                .WithHeader("Referer", referer.ToString())
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            var rootId = baseId == null ? $"ttg{tortugaId}" : $"{baseId}_{tortugaId}";
            var fileNode = fileRegex.Match(text ?? "").Groups["file"].Value.NotEmptyOrNull()
                ?? serialRegex.Match(text ?? "").Groups["file"].Value;

            return ProviderHelper.ParsePlaylistFromPlayerJsString(siteProvider.HttpClient, siteProvider.Site, rootId, fileNode, translationTitle, title, tortugaLink, LocalizationHelper.UaLang);
        }
    }
}

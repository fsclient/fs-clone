namespace FSClient.UWP.Shared.Selectors
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Models;

    public class NodeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? FileTemplate { get; set; }
        public DataTemplate? FolderTemplate { get; set; }
        public DataTemplate? TorrentFolderTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(
            object item, DependencyObject container)
        {
            return item switch
            {
                File _ when FileTemplate != null => FileTemplate,
                TorrentFolder _ when TorrentFolderTemplate != null => TorrentFolderTemplate,
                Folder _ when FolderTemplate != null => FolderTemplate,
                _ => base.SelectTemplateCore(item, container),
            };
        }
    }
}

namespace FSClient.UWP.Shared.Views.Controls
{
#if WINUI3
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml.Controls;
#endif

    public partial class MediaPlaylist : ListView
    {
        public MediaPlaylist()
        {
            DefaultStyleKey = nameof(MediaPlaylist);
        }

        protected override void OnItemsChanged(object e)
        {
            base.OnItemsChanged(e);
            var item = SelectedItem;
            if (item != null)
            {
                ScrollIntoView(item);
            }
        }
    }
}

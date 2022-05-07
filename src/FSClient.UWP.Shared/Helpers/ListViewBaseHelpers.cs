namespace FSClient.UWP.Shared.Helpers
{
    using System.Threading.Tasks;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    public static class ListViewBaseHelpers
    {
        public static async Task<bool> ScrollAndFocusItemAsync(this ListViewBase listView, object item)
        {
            await listView.WaitForLoadedAsync();

            listView.ScrollIntoView(item, ScrollIntoViewAlignment.Leading);

            if (listView.ContainerFromItem(item) is Control control
                && await control.TryFocusAsync(FocusState.Keyboard).ConfigureAwait(true))
            {
                return true;
            }

            await Task.Delay(10);

            var tcs = new TaskCompletionSource<bool>();
            listView.LayoutUpdated += ListViewLayoutUpdated;

            async void ListViewLayoutUpdated(object _, object __)
            {
                listView.LayoutUpdated -= ListViewLayoutUpdated;
                if (listView.ContainerFromItem(item) is Control inControl)
                {
                    var result = await inControl.TryFocusAsync(FocusState.Keyboard).ConfigureAwait(false);
                    tcs.TrySetResult(result);
                }
                else
                {
                    tcs.TrySetResult(false);
                }
            }

            return await tcs.Task.ConfigureAwait(false);
        }
    }
}

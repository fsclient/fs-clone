namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

#if WINUI3
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.UWP.Shared.Helpers;

    public static class DialogHelpers
    {
        public static ValueTask HideAsync(this ContentDialog contentDialog)
        {
            return contentDialog.Dispatcher.CheckBeginInvokeOnUI(contentDialog.Hide);
        }

        public static async Task<ContentDialogResult> ShowAsync(this ContentDialog contentDialog,
            CancellationToken cancellationToken)
        {
            // It is better to use 'useSynchronizationContext = true', when it will work as expected with WinRT SyncContext
            // See https://github.com/dotnet/coreclr/issues/21183
            using (cancellationToken.Register(async state => await ((ContentDialog?)state)!.HideAsync(), contentDialog))
            {
                return await contentDialog.ShowAsync();
            }
        }
    }
}

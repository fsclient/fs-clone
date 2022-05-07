namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Nito.AsyncEx;

    internal static class LazyDialog
    {
        internal static readonly SemaphoreSlim ShowingSemaphore = new SemaphoreSlim(1);

        internal static Task<TDialog> DialogFactory<TDialog>()
            where TDialog : new()
        {
            return DispatcherHelper.GetForCurrentOrMainView()?
                       .CheckBeginInvokeOnUI(() => Task.FromResult(new TDialog()))
                   ?? Task.FromResult(new TDialog());
        }
    }

    public class LazyDialog<TDialog, TInput, TOutput> : IContentDialog<TInput, TOutput>
        where TDialog : IContentDialog<TInput, TOutput>, new()
    {
        private readonly AsyncLazy<TDialog> dialog
            = new AsyncLazy<TDialog>(LazyDialog.DialogFactory<TDialog>);

        public async Task<TOutput> ShowAsync(TInput arg, CancellationToken cancellationToken)
        {
            using (await LazyDialog.ShowingSemaphore.LockAsync(cancellationToken).ConfigureAwait(true))
            {
                var result = await (await dialog).ShowAsync(arg, cancellationToken).ConfigureAwait(false);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                return result;
            }
        }
    }

    public class LazyDialog<TDialog, TOutput> : IContentDialog<TOutput>
        where TDialog : IContentDialog<TOutput>, new()
    {
        private readonly AsyncLazy<TDialog> dialog
            = new AsyncLazy<TDialog>(LazyDialog.DialogFactory<TDialog>);

        public async Task<TOutput> ShowAsync(CancellationToken cancellationToken)
        {
            using (await LazyDialog.ShowingSemaphore.LockAsync(cancellationToken))
            {
                var result = await (await dialog).ShowAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                return result;
            }
        }
    }

    public class LazyDialog<TDialog> : IContentDialog
        where TDialog : IContentDialog, new()
    {
        private readonly AsyncLazy<TDialog> dialog
            = new AsyncLazy<TDialog>(LazyDialog.DialogFactory<TDialog>);

        public async Task ShowAsync(CancellationToken cancellationToken)
        {
            using (await LazyDialog.ShowingSemaphore.LockAsync(cancellationToken))
            {
                await (await dialog).ShowAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

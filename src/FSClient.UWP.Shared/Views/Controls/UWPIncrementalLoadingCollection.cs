namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Foundation;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Data;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Data;
#endif

    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;

    public class IncrementalLoadingCollectionFactory
        : IIncrementalCollectionFactory
    {
        public IIncrementalCollection<TSource> Create<TSource>(
            IAsyncEnumerable<TSource> source)
        {
            return new UWPIncrementalLoadingCollection<TSource, TSource>(source);
        }

        public IIncrementalCollection<TSource, TElement> Create<TSource, TElement>(
            IAsyncEnumerable<TSource> source,
            Func<IEnumerable<TSource>, IEnumerable<TElement>> functor)
        {
            return new UWPIncrementalLoadingCollection<TSource, TElement>(source, functor);
        }
    }

    public class UWPIncrementalLoadingCollection<TSource, TElement>
        : IncrementalLoadingCollection<TSource, TElement>, ISupportIncrementalLoading
    {
        public UWPIncrementalLoadingCollection(
            IAsyncEnumerable<TElement> source)
            : base(source)
        {
        }

        public UWPIncrementalLoadingCollection(
            IAsyncEnumerable<TSource> source,
            Func<IEnumerable<TSource>, IEnumerable<TElement>> functor)
            : base(source, functor)
        {
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint fetchCount)
        {
            return InternalLoadMoreItemsAsync(CancellationToken.None).AsAsyncOperation();

            async Task<LoadMoreItemsResult> InternalLoadMoreItemsAsync(CancellationToken passedToken)
            {
                // Workaround for bug https://github.com/microsoft/microsoft-ui-xaml/issues/723
                if (Count > 0 && fetchCount <= 1)
                {
                    return new LoadMoreItemsResult {Count = 0};
                }

                var approxItemsCount = Window.Current is Window window
                    ? (uint)((window.Bounds.Width * window.Bounds.Height) / (220 * 150))
                    : 10;

                var actualfetchCount = fetchCount < 10
                    ? approxItemsCount
                    : Math.Min(60, Math.Min(approxItemsCount, fetchCount));

                try
                {
                    var resultCount = await FetchAsync((int)actualfetchCount, passedToken).ConfigureAwait(true);
                    return new LoadMoreItemsResult {Count = (uint)resultCount};
                }
                catch (Exception ex)
                {
                    HasMoreItems = false;

                    if (!(ex is OperationCanceledException))
                    {
                        Logger.Instance.LogError(ex);
                    }

                    return new LoadMoreItemsResult {Count = 0};
                }
            }
        }
    }
}

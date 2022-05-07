namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Services;

    public interface IIncrementalCollection : IEnumerable
    {
        bool IsLoading { get; }
        bool HasMoreItems { get; }
        bool HasAnyItem { get; }
        int Count { get; }

        void Reset();

        Task<int> FetchAsync(
            int count = IncrementalLoadingCollection.DefaultCount,
            CancellationToken cancellationToken = default);
    }

    public interface IIncrementalCollection<TSource, TElement>
        : IIncrementalCollection<TElement>
    {
    }

    public interface IIncrementalCollection<TElement>
        : IIncrementalCollection, IEnumerable<TElement>
    {
    }

    public abstract class IncrementalLoadingCollection<TSource, TElement>
        : SafeObservableCollection<TSource, TElement>, IIncrementalCollection<TSource, TElement>
    {
        private const int FunctorBufferSize = 16;

        private bool isEnded;
        private int isBusy; // 0 = false

        private readonly IAsyncEnumerable<TElement> source;
        private IAsyncEnumerator<TElement>? enumerator;

        protected IncrementalLoadingCollection()
        {
            source = AsyncEnumerable.Empty<TElement>();
            isEnded = false;
            MaxItems = 0;
        }

        protected IncrementalLoadingCollection(IAsyncEnumerable<TElement> source)
            : this()
        {
            this.source = source;
        }

        protected IncrementalLoadingCollection(IAsyncEnumerable<TSource> source, Func<IEnumerable<TSource>, IEnumerable<TElement>> functor)
            : this()
        {
            this.source = source.Buffer(FunctorBufferSize).SelectMany(buffer => functor(buffer).ToAsyncEnumerable());
        }

        public bool HasMoreItems
        {
            get
            {
                if (isEnded && !IsInfinity)
                {
                    return false;
                }

                return MaxItems == 0 || MaxItems > Count;
            }
            protected set => isEnded = value;
        }

        public uint MaxItems { get; protected set; }

        public bool IsInfinity { get; protected set; }

        public bool IsLoading { get; private set; }

        public virtual async Task<int> FetchAsync(int count, CancellationToken cancellationToken = default)
        {
            if (count == 0
                || (MaxItems > 0 && Count >= MaxItems))
            {
                return 0;
            }

            var busy = Interlocked.Exchange(ref isBusy, 1) == 1;
            if (busy)
            {
                return 0;
            }

            try
            {
                IsLoading = true;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsLoading)));

                var (items, hasNextPage) = await FetchNextAsync(count, cancellationToken).ConfigureAwait(true);
                if (items == null)
                {
                    isEnded = true;
                    return 0;
                }

                var list = items.ToList();

                isEnded = !hasNextPage;

                AddRange(list);

                OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasMoreItems)));

                return list.Count;
            }
            finally
            {
                IsLoading = false;
                Interlocked.Exchange(ref isBusy, 0);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsLoading)));
            }
        }

        public async void Reset()
        {
            if (enumerator != null)
            {
                await enumerator.DisposeAsync().ConfigureAwait(true);
            }
            enumerator = null;
            ClearItems();
        }

        protected override void ClearItems()
        {
            isEnded = false;
            if (Count > 0)
            {
                base.ClearItems();
            }
        }

        private async Task<(IEnumerable<TElement>, bool hasNextPage)> FetchNextAsync(int count, CancellationToken cancellationToken)
        {
            var list = new List<TElement>(count);

            try
            {
                enumerator ??= source
                    .GetAsyncEnumerator(cancellationToken)
                    .WithCancellation(cancellationToken);

                while (count-- > 0
                    && await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    list.Add(enumerator.Current);
                }

                if (list.Count == 0)
                {
                    await enumerator.DisposeAsync().ConfigureAwait(true);
                    enumerator = null;
                }
            }
            catch (OperationCanceledException)
            {
                return (list, false);
            }
            catch (HttpRequestException ex)
            {
                Logger.Instance.LogWarning(ex);
                return (list, false);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }

            return (list, list.Count > 0);
        }
    }

    public static class IncrementalLoadingCollection
    {
        public const int DefaultCount = 16;

        public static IIncrementalCollection<TElement> Empty<TElement>()
        {
            return default(EmptyCollection<TElement, TElement>);
        }

        private readonly struct EmptyCollection<TSource, TElement>
            : IIncrementalCollection<TSource, TElement>
        {
            public bool IsLoading => false;

            public bool HasMoreItems => false;

            public bool HasAnyItem => false;

            public int Count => 0;

            public Task<int> FetchAsync(int count = 16, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0);
            }

            public IEnumerator<TElement> GetEnumerator()
            {
                yield break;
            }

            public void Reset()
            {

            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                yield break;
            }
        }

        public static Task<int> FetchAsync<TElement>(
            this IIncrementalCollection<TElement> collection, CancellationToken cancellationToken = default)
        {
            return collection.FetchAsync(DefaultCount, cancellationToken);
        }
    }
}

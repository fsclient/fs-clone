namespace FSClient.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    public static class EnumerableHelper
    {
        public static int IndexOf<TInput>(this IEnumerable<TInput> source, TInput item)
        {
            if (source is IList<TInput> list)
            {
                return list.IndexOf(item);
            }
            var foundedTuple = source
                .Select((itm, ind) => (founded: itm?.Equals(item) ?? item is null, ind))
                .FirstOrDefault(t => t.founded);
            return foundedTuple.founded ? foundedTuple.ind : -1;
        }

        [return: MaybeNull]
        public static TKey MaxOrDefault<TInput, TKey>(this IEnumerable<TInput> source, Func<TInput, TKey> keySelector)
        {
            return source.Select(keySelector).OrderByDescending(k => k).FirstOrDefault();
        }

        [return: MaybeNull]
        public static TKey MinOrDefault<TInput, TKey>(this IEnumerable<TInput> source, Func<TInput, TKey> keySelector)
        {
            return source.Select(keySelector).OrderBy(k => k).FirstOrDefault();
        }

        public static IEnumerable<TInput> DistinctBy<TInput, TKey>(this IEnumerable<TInput> source, Func<TInput, TKey> keySelector)
        {
            return source.Distinct(new LamdbaPropertyComparer<TInput, TKey>(keySelector));
        }

        public static IAsyncEnumerable<TInput> DistinctBy<TInput, TKey>(this IAsyncEnumerable<TInput> source, Func<TInput, TKey> keySelector)
        {
            return source.Distinct(new LamdbaPropertyComparer<TInput, TKey>(keySelector));
        }

        public static async IAsyncEnumerable<TOutput> ToAsyncEnumerable<TOutput>(this Func<CancellationToken, Task<TOutput>> source,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return await source(cancellationToken).ConfigureAwait(false);
        }

        public static async IAsyncEnumerable<TOutput> ToEmptyAsyncEnumerable<TOutput>(this Task source)
        {
            await source.ConfigureAwait(false);
            yield break;
        }

        public static async Task<IEnumerable<TInput>> ToEnumerableAsync<TInput>(this IAsyncEnumerable<TInput> source, CancellationToken cancellationToken)
        {
            return await source.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        public static async Task<TInput[]> ToArrayAsync<TInput>(this Task<IEnumerable<TInput>> source)
        {
            var result = await source.ConfigureAwait(false);
            return result.ToArray();
        }

        public static IAsyncEnumerable<TInput> ToFlatAsyncEnumerable<TInput>(this Task<IEnumerable<TInput>> source)
        {
            return source.ToAsyncEnumerable().SelectMany(single => single.ToAsyncEnumerable());
        }

        public static IAsyncEnumerable<TResult> WhenAll<TInput, TResult>(
            this IAsyncEnumerable<TInput> tasks,
            Func<TInput, CancellationToken, Task<TResult>> selector)
        {
            return WhenAllInternal(tasks, selector);
        }

        private static async IAsyncEnumerable<TResult> WhenAllInternal<TInput, TResult>(
            this IAsyncEnumerable<TInput> tasks,
            Func<TInput, CancellationToken, Task<TResult>> selector,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var elements = await tasks.ToArrayAsync(cancellationToken).ConfigureAwait(false);
            var results = await Task.WhenAll(elements.Select(el => selector(el, cancellationToken))).ConfigureAwait(false);

            foreach (var result in results)
            {
                yield return result;
            }
        }

        public static async Task WhenAllAsync<TInput>(
            this IAsyncEnumerable<TInput> tasks,
            Func<TInput, CancellationToken, Task> selector,
            CancellationToken cancellationToken = default)
        {
            var elements = await tasks.ToArrayAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(elements.Select(el => selector(el, cancellationToken))).ConfigureAwait(false);
        }

        public static IAsyncEnumerable<TResult> SelectBatchAwait<TInput, TResult>(
            this IAsyncEnumerable<TInput> source, int size,
            Func<TInput, CancellationToken, Task<TResult>> selector)
        {
            return AsyncEnumerable.Create(ct =>
            {
                var bufferedEnumerator = source.Buffer(size).GetAsyncEnumerator(ct);
                IEnumerator<TResult>? batchEnumerator = default;
                TResult current = default!;

                return AsyncEnumerator.Create(
                    async () =>
                    {
                        if (batchEnumerator?.MoveNext() != true)
                        {
                            batchEnumerator?.Dispose();
                            batchEnumerator = null;

                            var hasNext = await bufferedEnumerator.MoveNextAsync(ct).ConfigureAwait(false);
                            if (!hasNext)
                            {
                                return false;
                            }

                            batchEnumerator = (await Task.WhenAll(bufferedEnumerator.Current
                                .Select(item => selector(item, ct)))
                                .ConfigureAwait(false))
                                .AsEnumerable()
                                .GetEnumerator();

                            if (batchEnumerator.MoveNext())
                            {
                                current = batchEnumerator.Current;
                                return true;
                            }
                            return false;
                        }
                        else
                        {
                            current = batchEnumerator.Current;
                            return true;
                        }
                    },
                    () => current,
                    () =>
                    {
                        batchEnumerator?.Dispose();
                        return bufferedEnumerator.DisposeAsync();
                    });
            });
        }

        private readonly struct LamdbaPropertyComparer<TInput, TKey> : IEqualityComparer<TInput>
        {
            private readonly Func<TInput, TKey> expr;

            public LamdbaPropertyComparer(Func<TInput, TKey> expr)
            {
                this.expr = expr;
            }

            public bool Equals([AllowNull] TInput left, [AllowNull] TInput right)
            {
                var leftProp = expr.Invoke(left!);
                var rightProp = expr.Invoke(right!);

                if (leftProp is null && rightProp is null)
                {
                    return true;
                }
                else
                {
                    return leftProp?.Equals(rightProp) ?? false;
                }
            }

            public int GetHashCode(TInput obj)
            {
                var prop = expr.Invoke(obj);
                return (prop == null) ? 0 : prop.GetHashCode();
            }
        }
    }
}

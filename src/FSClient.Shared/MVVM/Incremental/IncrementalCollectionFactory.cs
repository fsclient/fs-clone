namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public interface IIncrementalCollectionFactory
    {
        IIncrementalCollection<TSource> Create<TSource>(
            IAsyncEnumerable<TSource> source);

        IIncrementalCollection<TSource, TElement> Create<TSource, TElement>(
            IAsyncEnumerable<TSource> source,
            Func<IEnumerable<TSource>, IEnumerable<TElement>> functor);
    }

    public static class IncrementalCollectionFactoryHelper
    {
        public static IIncrementalCollection<TSource, TGrouping> CreateGrouped<TSource, TKey, TGrouping>(
            this IIncrementalCollectionFactory incrementalCollectionFactory,
            Func<TSource, TKey> keySelector,
            IAsyncEnumerable<TSource> source)
            where TGrouping : IIncrementalGrouping<TKey, TSource>, new()
        {
            return incrementalCollectionFactory.Create(
                source,
                source => source
                    .GroupBy(keySelector, i => i)
                    .Select(g => new TGrouping
                    {
                        Key = g.Key,
                        Values = g.ToList()
                    }));
        }
    }
}

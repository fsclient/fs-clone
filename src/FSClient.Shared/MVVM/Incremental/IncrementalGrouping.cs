namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;

    public interface IIncrementalGrouping<TElement> : IEnumerable<TElement>
    {
        object Key { get; }
        List<TElement> Values { get; set; }

        void Concat(IEnumerable<TElement> other);
    }

    public interface IIncrementalGrouping<TKey, TElement> : IIncrementalGrouping<TElement>, IGrouping<TKey, TElement>
    {
        new TKey Key { get; set; }
    }

    public abstract class IncrementalGrouping<TKey, TElement> : IIncrementalGrouping<TKey, TElement>, INotifyCollectionChanged
    {
        private readonly ContextSafeEvent<NotifyCollectionChangedEventHandler> collectionChangedEvent;

        protected IncrementalGrouping()
        {
            collectionChangedEvent = new ContextSafeEvent<NotifyCollectionChangedEventHandler>();
            Key = default!;
            Values = default!;
        }

        public TKey Key { get; set; }
        public List<TElement> Values { get; set; }
        object IIncrementalGrouping<TElement>.Key => Key!;

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add => collectionChangedEvent.Register(value);
            remove => collectionChangedEvent.Unregister(value);
        }

        public void Concat(IEnumerable<TElement> other)
        {
            var lastCount = Values?.Count ?? 0;
            var newItems = other.ToList();
            if (Values != null)
            {
                Values.AddRange(newItems);
            }
            else
            {
                Values = new List<TElement>(other);
            }
            collectionChangedEvent.Invoke(handler => handler.Invoke(this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    newItems,
                    Array.Empty<TElement>(),
                    lastCount)));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TElement> GetEnumerator()
        {
            return (Values ?? Enumerable.Empty<TElement>()).GetEnumerator();
        }
    }
}

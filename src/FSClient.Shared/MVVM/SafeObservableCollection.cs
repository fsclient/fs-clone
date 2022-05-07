namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;

    public class SafeObservableCollection<TElement>
        : SafeObservableCollection<TElement, TElement>
    {

    }

    public class SafeObservableCollection<TSource, TElement>
        : ObservableCollection<TElement>, INotifyPropertyChanged
    {
        private bool suspendCollectionChangeNotification;

        private readonly object locker = new object();

        private readonly ContextSafeEvent<PropertyChangedEventHandler> propertyChangedEvent;
        private readonly ContextSafeEvent<NotifyCollectionChangedEventHandler> collectionChangedEvent;

        public SafeObservableCollection()
        {
            propertyChangedEvent = new ContextSafeEvent<PropertyChangedEventHandler>();
            collectionChangedEvent = new ContextSafeEvent<NotifyCollectionChangedEventHandler>();
        }

        public override event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add => collectionChangedEvent.Register(value);
            remove => collectionChangedEvent.Unregister(value);
        }

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add => propertyChangedEvent.Register(value);
            remove => propertyChangedEvent.Unregister(value);
        }

        public bool HasAnyItem => Count > 0;

        public void AddRange(IEnumerable<TElement> range)
        {
            var list = range.ToList();
            if (list.Count > 0)
            {
                lock (locker)
                {
                    var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list, Count);
                    suspendCollectionChangeNotification = true;

                    if (list.FirstOrDefault() is IIncrementalGrouping<TSource>)
                    {
                        var lastGroup = (IIncrementalGrouping<TSource>?)this.LastOrDefault();
                        args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
                        foreach (var item in list)
                        {
                            if (item == null)
                            {
                                continue;
                            }

                            var group = (IIncrementalGrouping<TSource>)item;
                            if (lastGroup?.Key?.Equals(group.Key) == true)
                            {
                                lastGroup.Concat(group);
                            }
                            else
                            {
                                Insert(Count, item);
                                lastGroup = group;
                            }
                        }
                    }
                    else
                    {
                        foreach (var item in list)
                        {
                            Insert(Count, item);
                        }
                    }
                    suspendCollectionChangeNotification = false;
                    OnCollectionChanged(args);
                }

                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)), new PropertyChangedEventArgs(nameof(HasAnyItem)), new PropertyChangedEventArgs("Item[]"));
            }
        }

        protected override async void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (suspendCollectionChangeNotification)
            {
                return;
            }

            var blocker = BlockReentrancy();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            try
            {
                if (suspendCollectionChangeNotification)
                {
                    return;
                }

                await collectionChangedEvent.InvokeAsync(handler => handler.Invoke(this, e), cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ingore
            }
            finally
            {
                cts.Dispose();
                blocker.Dispose();
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (suspendCollectionChangeNotification)
            {
                return;
            }

            propertyChangedEvent.Invoke(handler => handler.Invoke(this, e));
        }

        protected void OnPropertyChanged(params PropertyChangedEventArgs[] e)
        {
            if (suspendCollectionChangeNotification)
            {
                return;
            }

            propertyChangedEvent.Invoke(handler =>
            {
                foreach (var args in e)
                {
                    handler.Invoke(this, args);
                }
            });
        }
    }
}

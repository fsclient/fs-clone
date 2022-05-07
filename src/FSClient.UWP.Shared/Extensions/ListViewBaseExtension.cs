namespace FSClient.UWP.Shared.Extensions
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Threading.Tasks;

    using Windows.UI.Core;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Data;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Data;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    public static class ListViewBaseExtension
    {
        private static bool ignoreCollectionChanged;

        public static readonly DependencyProperty IncrementalSourceProperty =
            DependencyProperty.Register("IncrementalSource", typeof(IIncrementalCollection),
                typeof(ListViewBaseExtension),
                new PropertyMetadata(null, OnIncrementalLoadingChanged));

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register("SelectedItems", typeof(ICollection<object>), typeof(ListViewBaseExtension),
                new PropertyMetadata(null, OnSelectedItemsChanged));

        public static IIncrementalCollection? GetIncrementalSource(DependencyObject dependencyObject)
        {
            return dependencyObject.GetValue(IncrementalSourceProperty) as IIncrementalCollection;
        }

        public static void SetIncrementalSource(DependencyObject dependencyObject, IIncrementalCollection? value)
        {
            dependencyObject.SetValue(IncrementalSourceProperty, value);
        }

        public static ICollection<object?>? GetSelectedItems(DependencyObject dependencyObject)
        {
            return dependencyObject.GetValue(SelectedItemsProperty) as ICollection<object?>;
        }

        public static void SetSelectedItems(DependencyObject dependencyObject, ICollection<object?>? value)
        {
            dependencyObject.SetValue(SelectedItemsProperty, value);
        }

        private static async void OnIncrementalLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (d is ItemsControl list)
                {
                    await list.WaitForLoadedAsync();
                    if (list.FindVisualAscendant<ScrollViewer>() is not ScrollViewer scrollViewer)
                    {
                        return;
                    }

                    scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
                    list.DataContext = null;
                    list.ItemsSource = null;

                    if (e.NewValue is IIncrementalCollection incrementalCollection)
                    {
                        scrollViewer.ViewChanged += ScrollViewer_ViewChanged;

                        var source = new CollectionViewSource {IsSourceGrouped = true, Source = incrementalCollection};
                        list.DataContext = source;
                        list.ItemsSource = source.View;

                        await incrementalCollection.FetchAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }

            async void ScrollViewer_ViewChanged(object sender, object __)
            {
                try
                {
                    var collection = GetIncrementalSource(d);
                    if (collection == null)
                    {
                        SetIncrementalSource(d, collection = IncrementalLoadingCollection.Empty<object?>());
                    }

                    var scrollViewer = (ScrollViewer)sender;

                    if (!collection.HasMoreItems)
                    {
                        scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
                    }

                    while (!collection.IsLoading
                           && collection.HasMoreItems
                           && (scrollViewer.VerticalOffset / scrollViewer.ScrollableHeight) > 0.95)
                    {
                        await collection.FetchAsync().ConfigureAwait(true);
                        await Task.Delay(100).ConfigureAwait(true);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError(ex);
                }
            }
        }

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListViewBase listViewBase)
            {
                listViewBase.SelectionChanged -= ListViewBase_SelectionChanged;
                if (e.OldValue is ObservableCollection<object> oldObservable)
                {
                    oldObservable.CollectionChanged -= Observable_CollectionChanged;
                }

                if (e.NewValue is ICollection collection)
                {
                    listViewBase.SelectionChanged += ListViewBase_SelectionChanged;
                    if (collection is ObservableCollection<object> observable)
                    {
                        observable.CollectionChanged += Observable_CollectionChanged;
                    }
                }
            }

            void ListViewBase_SelectionChanged(object sender, SelectionChangedEventArgs args)
            {
                if (listViewBase.SelectionMode == ListViewSelectionMode.Multiple)
                {
                    ignoreCollectionChanged = true;
                    var collection = GetSelectedItems(listViewBase);
                    if (collection == null)
                    {
                        SetSelectedItems(listViewBase, collection = Array.Empty<object?>());
                    }

                    foreach (var item in args.RemovedItems)
                    {
                        collection.Remove(item);
                    }

                    foreach (var item in args.AddedItems)
                    {
                        if (!collection.Contains(item))
                        {
                            collection.Add(item);
                        }
                    }

                    ignoreCollectionChanged = false;
                }
            }

            void Observable_CollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
            {
                _ = listViewBase.Dispatcher.RunIdleAsync(new IdleDispatchedHandler(_ =>
                {
                    if (ignoreCollectionChanged)
                    {
                        return;
                    }
                    try
                    {
                        if (args.Action == NotifyCollectionChangedAction.Reset
                            && listViewBase.SelectedItems.Count > 0)
                        {
                            listViewBase.SelectedItems.Clear();
                            return;
                        }

                        if (args.OldItems != null
                            && listViewBase.SelectionMode == ListViewSelectionMode.Multiple)
                        {
                            foreach (var item in args.OldItems)
                            {
                                listViewBase.SelectedItems.Remove(item);
                            }
                        }

                        if (args.NewItems != null
                            && listViewBase.SelectionMode == ListViewSelectionMode.Multiple)
                        {
                            foreach (var item in args.NewItems)
                            {
                                var index = listViewBase.SelectedItems.IndexOf(item);
                                if (index < 0)
                                {
                                    listViewBase.SelectedItems.Add(item);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogWarning(ex);
                    }
                }));
            }
        }
    }
}

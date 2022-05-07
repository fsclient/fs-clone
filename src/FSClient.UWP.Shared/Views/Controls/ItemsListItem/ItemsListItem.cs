namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

#if UNO
    using Windows.UI.Text;
#elif WINUI3
    using Microsoft.UI.Text;
#endif
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Documents;
    using Microsoft.UI.Xaml.Input;
#else
    using Windows.UI.Text;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
    using Windows.UI.Xaml.Documents;
#endif

    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.Localization.Resources;

    public partial class ItemsListItem : Control
    {
        private const int ItemPreloadingTimeout = 500;

        private static readonly Timer commandExecutingTimer =
            new Timer(ExecuteCommandOnTimer, null, 0, Timeout.Infinite);

        private static ExecutionContext? nextExecurtionContext;

        private readonly MenuFlyoutItem removeFromHistoryItem;

        public ItemsListItem()
        {
            DefaultStyleKey = nameof(ItemsListItem);
            removeFromHistoryItem = new MenuFlyoutItem() { Text = Strings.ItemsListItem_RemoveFromHistory };
        }

        protected override void OnApplyTemplate()
        {
            if (GetTemplateChild("DisplayItemModesGroup") is VisualStateGroup visualStateGroup)
            {
                visualStateGroup.CurrentStateChanged += VisualStateGroup_CurrentStateChanged;
            }

            if (GetTemplateChild("RootGrid") is Grid rootGrid)
            {
                rootGrid.Loaded += SetupItemGrid;
            }

            EnsureStates();

            base.OnApplyTemplate();
        }

        private void EnsureStates()
        {
            var historyItem = HistoryItem;
            var hasHistory = historyItem != null;

            VisualStateManager.GoToState(this,
                (hasHistory, historyItem?.Season.HasValue, historyItem?.Episode.HasValue) switch
                {
                    (true, true, true) => "SeasonAndEpisodeState",
                    (true, true, false) => "SeasonState",
                    (true, false, true) => "EpisodeState",
                    _ => "NoSeasonAndEpisodeState"
                }, false);

            VisualStateManager.GoToState(this, nameof(DisplayItemMode) + DisplayItemMode.ToString(), false);

            if (GetTemplateChild("ContextMenu") is FavMenuFlyout contextMenu)
            {
                contextMenu.AdditionalItems.Remove(removeFromHistoryItem);
                if (hasHistory && DeleteFromHistoryCommand is ICommand command)
                {
                    removeFromHistoryItem.CommandParameter = historyItem;
                    removeFromHistoryItem.Command = command;
                    contextMenu.AdditionalItems.Add(removeFromHistoryItem);
                }
            }
        }

        private void VisualStateGroup_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            if (e.NewState.Name.EndsWith(DisplayItemMode.Detailed.ToString(), StringComparison.Ordinal)
                && GetTemplateChild("TagsBlock") is TextBlock tagsBlock)
            {
                tagsBlock.Loaded -= TagsBlockLoaded;
                tagsBlock.Loaded += TagsBlockLoaded;
            }
        }

        private static void TagsBlockLoaded(object sender, object args)
        {
            var textBlock = (TextBlock)sender;
            if (textBlock.Tag is not TagsContainer[] containers
                || containers.Length == 0)
            {
                textBlock.Visibility = Visibility.Collapsed;
                return;
            }

            var last = containers.LastOrDefault();
            textBlock.Inlines.Clear();
            foreach (var tags in containers)
            {
                textBlock.Inlines.Add(new Run {Text = tags.Title + ": ", FontWeight = FontWeights.Bold});
                textBlock.Inlines.Add(new Run {Text = string.Join(", ", tags.Items.Select(t => t.Title))});
                if (tags != last)
                {
                    textBlock.Inlines.Add(new LineBreak());
                }
            }

            textBlock.Visibility = Visibility.Visible;
            textBlock.Loaded -= TagsBlockLoaded;
        }

        private static void SetupItemGrid(object sender, RoutedEventArgs e)
        {
            var grid = (Grid)sender;
            var itemsListItem = grid.FindVisualAscendant<ItemsListItem>();
            var gridViewItem = itemsListItem?.FindVisualAscendant<GridViewItem>();
            if (gridViewItem == null)
            {
                return;
            }

            gridViewItem.PointerEntered += ShowAnim;
            gridViewItem.PointerExited += HideAnim;
            gridViewItem.PointerCaptureLost += HideAnim;

            gridViewItem.GotFocus += ShowAnim;
            gridViewItem.LostFocus += HideAnim;

            static void ShowAnim(object sender, object __)
            {
                var childItem = ((FrameworkElement)sender).FindVisualChild<ItemsListItem>();
                VisualStateManager.GoToState(childItem, "PreloadingState", true);
            }

            static void HideAnim(object sender, object __)
            {
                var childItem = ((FrameworkElement)sender).FindVisualChild<ItemsListItem>();
                VisualStateManager.GoToState(childItem, "NoPreloadingState", false);
            }
        }


        private static void ItemContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is SelectorItem selectorItem
                && e.GetCurrentPoint(selectorItem).Properties is var props
                && !props.IsMiddleButtonPressed
                && selectorItem.FindVisualChild<ItemsListItem>() is ItemsListItem itemsListItem
                && itemsListItem.ItemInfo is ItemInfo itemInfo
                && itemsListItem.ItemPreloadCommand is ICommand command
                && command.CanExecute(itemInfo))
            {
                nextExecurtionContext = new ExecutionContext(command, selectorItem.Content, itemsListItem);
                commandExecutingTimer.Change(ItemPreloadingTimeout, Timeout.Infinite);
            }
        }

        private static void ItemContainer_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is SelectorItem selectorItem
                && selectorItem.FindVisualChild<ItemsListItem>() is ItemsListItem itemsListItem
                && itemsListItem.ItemInfo is ItemInfo itemInfo
                && itemsListItem.ItemPreloadCommand is ICommand command
                && command.CanExecute(itemInfo))
            {
                nextExecurtionContext = new ExecutionContext(command, selectorItem.Content, itemsListItem);
                commandExecutingTimer.Change(ItemPreloadingTimeout, Timeout.Infinite);
            }
        }

        private static async void ExecuteCommandOnTimer(object _)
        {
            var currentContext = nextExecurtionContext;
            if (currentContext != null)
            {
                nextExecurtionContext = null;

                if (currentContext.Command is IAsyncCommand asyncCommand)
                {
                    await asyncCommand.ExecuteAsync(currentContext.CommandParameter).ConfigureAwait(false);
                }
                else
                {
                    currentContext.Command.Execute(currentContext.CommandParameter);
                }

                await currentContext.Control.Dispatcher.CheckBeginInvokeOnUI(async () =>
                {
                    var itemInfo = currentContext.Control.ItemInfo;
                    currentContext.Control.ItemInfo = null!;
                    await Task.Yield();
                    currentContext.Control.ItemInfo = itemInfo;
                }).ConfigureAwait(false);
            }
        }

        private class ExecutionContext
        {
            public ExecutionContext(ICommand command, object commandParameter, ItemsListItem control)
            {
                Command = command ?? throw new ArgumentNullException(nameof(command));
                CommandParameter = commandParameter;
                Control = control ?? throw new ArgumentNullException(nameof(control));
            }

            public ICommand Command { get; }

            public object CommandParameter { get; }

            public ItemsListItem Control { get; }
        }
    }
}

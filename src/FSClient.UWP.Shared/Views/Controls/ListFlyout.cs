namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Collections;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Data;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Data;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.UWP.Shared.Converters;
    using FSClient.UWP.Shared.Helpers;

    public class ListFlyout : FlyoutBase
    {
        private readonly ContentControl header;
        private readonly ListView list;
        private readonly FlyoutPresenter flyoutPresenter;

        private bool isOpened;
        private FocusState lastState;

        public ListFlyout()
        {
            Opened += (s, a) => isOpened = true;
            Closed += (s, a) => isOpened = false;

            header = new ContentControl
            {
                FontSize = 16,
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            header.SetBinding(
                UIElement.VisibilityProperty,
                new Binding {Source = header, Path = new PropertyPath("Content"), Converter = new BooleanConverter()});

            list = new ListView();
            list.IsItemClickEnabled = true;
            list.ItemClick += List_ItemClick;

            var scrollViewer = list.FindVisualChildren<ScrollViewer>().FirstOrDefault();
            if (scrollViewer != null)
            {
                scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            }

            list.GotFocus += (s, e) => lastState = (e.OriginalSource as Control)?.FocusState ?? default;
            list.LostFocus += (s, e) => lastState = (e.OriginalSource as Control)?.FocusState ?? default;
            list.SelectionChanged += (e, a) =>
            {
                var item = a.AddedItems.FirstOrDefault();
                var removed = a.RemovedItems.FirstOrDefault();
                if (item != null)
                {
                    if (!item.Equals(SelectedItem))
                    {
                        SelectedItem = item;
                    }

                    if (SelectCommand != null
                        && removed != null
                        && !item.Equals(removed)
                        && SelectCommand.CanExecute(item))
                    {
                        SelectCommand.Execute(item);
                    }
                }

                if (lastState != FocusState.Keyboard
                    && isOpened)
                {
                    Hide();
                }
            };

            var stack = new StackPanel();
            stack.Children.Add(header);
            stack.Children.Add(list);

            flyoutPresenter = new FlyoutPresenter
            {
                Content = stack, Padding = new Thickness(0, 8, 0, 8), MinWidth = 150
            };
        }

        #region Tag

        public static readonly DependencyProperty TagProperty =
            DependencyProperty.RegisterAttached("Tag", typeof(object), typeof(ListFlyout),
                new PropertyMetadata(null));

        public object? Tag
        {
            get => GetValue(TagProperty);
            set => SetValue(TagProperty, value);
        }

        #endregion

        #region SelectCommand

        public static readonly DependencyProperty SelectCommandProperty =
            DependencyProperty.RegisterAttached("SelectCommand", typeof(ICommand), typeof(ListFlyout),
                new PropertyMetadata(null));

        public ICommand? SelectCommand
        {
            get => (ICommand)GetValue(SelectCommandProperty);
            set => SetValue(SelectCommandProperty, value);
        }

        #endregion

        #region ClickCommand

        public static readonly DependencyProperty ClickCommandProperty =
            DependencyProperty.RegisterAttached("ClickCommand", typeof(ICommand), typeof(ListFlyout),
                new PropertyMetadata(null));

        public ICommand? ClickCommand
        {
            get => (ICommand?)GetValue(ClickCommandProperty);
            set => SetValue(ClickCommandProperty, value);
        }

        private async void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            await Task.Yield();

            if (ClickCommand?.CanExecute(e.ClickedItem) == true)
            {
                ClickCommand.Execute(e.ClickedItem);
            }
        }

        #endregion

        #region SelectionEnabled

        public static readonly DependencyProperty SelectionEnabledProperty =
            DependencyProperty.RegisterAttached("SelectionEnabled", typeof(bool), typeof(ListFlyout),
                new PropertyMetadata(true, SelectionEnabledChanged));

        public bool SelectionEnabled
        {
            get => (bool)GetValue(SelectionEnabledProperty);
            set => SetValue(SelectionEnabledProperty, value);
        }

        private static void SelectionEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var list = (d as ListFlyout)?.list;
            if (list != null)
            {
                list.SelectionMode = (bool)e.NewValue
                    ? ListViewSelectionMode.Single
                    : ListViewSelectionMode.None;
            }
        }

        #endregion

        #region Background

        public Brush? Background
        {
            get => (Brush?)flyoutPresenter.GetValue(Control.BackgroundProperty);
            set => flyoutPresenter.SetValue(Control.BackgroundProperty, value);
        }

        #endregion

        #region BorderBrush

        public Brush? BorderBrush
        {
            get => (Brush?)flyoutPresenter.GetValue(Control.BorderBrushProperty);
            set => flyoutPresenter.SetValue(Control.BorderBrushProperty, value);
        }

        #endregion

        #region MaxHeight

        public double MaxHeight
        {
            get => (double)flyoutPresenter.GetValue(FrameworkElement.MaxHeightProperty);
            set => flyoutPresenter.SetValue(FrameworkElement.MaxHeightProperty, value);
        }

        #endregion

        #region MaxWidth

        public double MaxWidth
        {
            get => (double)flyoutPresenter.GetValue(FrameworkElement.MaxWidthProperty);
            set => flyoutPresenter.SetValue(FrameworkElement.MaxWidthProperty, value);
        }

        #endregion

        #region MinHeight

        public double MinHeight
        {
            get => (double)flyoutPresenter.GetValue(FrameworkElement.MinHeightProperty);
            set => flyoutPresenter.SetValue(FrameworkElement.MinHeightProperty, value);
        }

        #endregion

        #region MinWidth

        public double MinWidth
        {
            get => (double)flyoutPresenter.GetValue(FrameworkElement.MinWidthProperty);
            set => flyoutPresenter.SetValue(FrameworkElement.MinWidthProperty, value);
        }

        #endregion

        #region Header

        public object? Header
        {
            get => header.GetValue(ContentControl.ContentProperty);
            set
            {
                header.SetValue(ContentControl.ContentProperty, value);
                header.Margin = value is string
                    ? new Thickness(12, 0, 12, 8)
                    : new Thickness(0);
            }
        }

        #endregion

        #region ItemsSource

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.RegisterAttached(
                "ItemsSource",
                typeof(IEnumerable),
                typeof(ListFlyout),
                new PropertyMetadata(null, ItemsSourceChanged));

        private static void ItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var list = (d as ListFlyout)?.list;
            if (list != null
                && e.NewValue != null)
            {
                list.ItemsSource = e.NewValue;
            }
        }

        #endregion

        #region SelectedItem

        public object? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItem",
                typeof(object),
                typeof(ListFlyout),
                new PropertyMetadata(null, SelectedItemChanged));

        private static void SelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var flyout = d as ListFlyout;
            var list = flyout?.list;
            if (list == null
                || e.NewValue?.Equals(e.OldValue) != false)
            {
                return;
            }

            if (!e.NewValue.Equals(list.SelectedItem))
            {
                list.SelectedItem = e.NewValue;
            }
        }

        #endregion

        #region ItemTemplate

        public DataTemplate? ItemTemplate
        {
            get => (DataTemplate?)list.GetValue(ItemsControl.ItemTemplateProperty);
            set => list.SetValue(ItemsControl.ItemTemplateProperty, value);
        }

        #endregion

        #region ItemTemplate

        public Style? ItemContainerStyle
        {
            get => (Style?)list.GetValue(ItemsControl.ItemContainerStyleProperty);
            set => list.SetValue(ItemsControl.ItemContainerStyleProperty, value);
        }

        #endregion

        protected override Control CreatePresenter()
        {
            return flyoutPresenter;
        }
    }
}

namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.UWP.Shared.Helpers;

    public class MenuFlyoutItemsContainerItem : MenuFlyoutItem
    {
        protected static readonly DependencyProperty FakeParentProperty =
            DependencyProperty.Register("FakeParent", typeof(MenuFlyoutItemsContainerItem),
                typeof(MenuFlyoutItemsContainerItem), new PropertyMetadata(null));

        private IList<MenuFlyoutItemBase>? parentItems;
        private MenuFlyoutContentItem? headerItem;
        private IEnumerable? lastRenderedSource;

        public MenuFlyoutItemsContainerItem()
        {
            DefaultStyleKey = nameof(MenuFlyoutItemsContainerItem);
            Loaded += MenuFlyoutItemsContainerItem_Loaded;
        }

        protected IList<MenuFlyoutItemBase>? ParentItems => (parentItems ??= Target switch
        {
            MenuFlyout flyout => flyout.Items,
            MenuFlyoutSubItem subItem => subItem.Items,
            null => null,
            _ => throw new InvalidOperationException("Invalid parent")
        });

        public DependencyObject Target
        {
            get => (DependencyObject)GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }

        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register(nameof(Target), typeof(DependencyObject), typeof(MenuFlyoutItemsContainerItem),
                new PropertyMetadata(null, OnTargetChanged));

        public object Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(object), typeof(MenuFlyoutItemsContainerItem),
                new PropertyMetadata(null, OnHeaderChanged));

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.RegisterAttached(nameof(ItemsSource), typeof(IEnumerable),
                typeof(MenuFlyoutItemsContainerItem), new PropertyMetadata(null, OnItemsSourceChanged));

        public DataTemplate? ItemTemplate
        {
            get => (DataTemplate?)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate),
                typeof(MenuFlyoutItemsContainerItem), new PropertyMetadata(null));

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MenuFlyoutItemsContainerItem)d;
            control.EnsureHeader();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MenuFlyoutItemsContainerItem)d).OnItemsSourceChanged();
        }

        private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MenuFlyoutItemsContainerItem)d).OnItemsSourceChanged();
            ((MenuFlyoutItemsContainerItem)d).EnsureHeader();
        }

        protected override void OnApplyTemplate()
        {
            OnItemsSourceChanged();
            EnsureHeader();
            base.OnApplyTemplate();
        }

        private void MenuFlyoutItemsContainerItem_Loaded(object sender, RoutedEventArgs e)
        {
            OnItemsSourceChanged();
            EnsureHeader();
        }

        protected virtual void OnItemsSourceChanged()
        {
            if (ParentItems == null)
            {
                return;
            }

            if (lastRenderedSource == ItemsSource)
            {
                return;
            }

            lastRenderedSource = ItemsSource;

            foreach (var menuItem in ParentItems.Cast<MenuFlyoutItemBase>().ToArray())
            {
                if (menuItem.GetValue(FakeParentProperty) == this)
                {
                    ParentItems.Remove(menuItem);
                }
            }

            if (ItemsSource is { } itemsSource)
            {
                var indexOfNext = ParentItems.IndexOf((MenuFlyoutItemBase?)headerItem ?? this) + 1;
                var visualParent = this.FindVisualAscendant<FrameworkElement>();

                foreach (var item in itemsSource.OfType<object>().Reverse())
                {
                    var menuItem = ItemTemplate is { } template
                        ? (MenuFlyoutItemBase)template.LoadContent()
                        : new MenuFlyoutContentItem {Content = new TextBlock {Text = item.ToString()}};
                    menuItem.DataContext = item;
                    menuItem.SetValue(FakeParentProperty, this);

                    ParentItems.Insert(indexOfNext, menuItem);
                }
            }
        }

        protected virtual void EnsureHeader()
        {
            if (ParentItems == null)
            {
                return;
            }

            var header = Header;

            if (header == headerItem?.Content)
            {
                return;
            }

            var indexOfThis = ParentItems.IndexOf(this);

            if (headerItem != null)
            {
                ParentItems.Remove(headerItem);
                headerItem = null;
            }

            if (header != null)
            {
                var item = new MenuFlyoutContentItem {Content = header};
                item.SetValue(FakeParentProperty, this);
                ParentItems.Insert(indexOfThis + 1, headerItem = item);
            }
        }
    }
}

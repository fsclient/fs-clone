namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;

    using Microsoft.UI.Xaml.Controls;
#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    public class RadioMenuFlyoutItemsContainerItem : MenuFlyoutItemsContainerItem
    {
        private bool isSourceChanging;

        public RadioMenuFlyoutItemsContainerItem()
        {
            DefaultStyleKey = nameof(RadioMenuFlyoutItemsContainerItem);
            Loaded += RadioMenuFlyoutItemsContainerItem_Loaded;
        }

        public object? SelectedCommandParameter
        {
            get => GetValue(SelectedCommandParameterProperty);
            set => SetValue(SelectedCommandParameterProperty, value);
        }

        public static readonly DependencyProperty SelectedCommandParameterProperty =
            DependencyProperty.Register(nameof(SelectedCommandParameter), typeof(object),
                typeof(RadioMenuFlyoutItemsContainerItem), new PropertyMetadata(null));

        public ICommand SelectedCommand
        {
            get => (ICommand)GetValue(SelectedCommandProperty);
            set => SetValue(SelectedCommandProperty, value);
        }

        public static readonly DependencyProperty SelectedCommandProperty =
            DependencyProperty.Register(nameof(SelectedCommand), typeof(ICommand),
                typeof(RadioMenuFlyoutItemsContainerItem), new PropertyMetadata(null));

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(RadioMenuFlyoutItemsContainerItem),
                new PropertyMetadata(null));

        protected override void OnItemsSourceChanged()
        {
            base.OnItemsSourceChanged();

            if (ParentItems == null)
            {
                return;
            }

            isSourceChanging = true;

            try
            {
                foreach (var item in ParentItems.OfType<RadioMenuFlyoutItem>())
                {
                    if (item.GetValue(FakeParentProperty) == this)
                    {
                        if (item.Tag == null)
                        {
                            item.Tag = item.RegisterPropertyChangedCallback(RadioMenuFlyoutItem.IsCheckedProperty,
                                OnRadioMenuFlyoutItemChecked);
                        }

                        var newValue = EqualityComparer<object>.Default.Equals(SelectedItem, item.DataContext);
                        if (newValue)
                        {
                            item.IsChecked = newValue;
                        }
                    }
                }
            }
            finally
            {
                isSourceChanging = false;
            }
        }

        private void RadioMenuFlyoutItemsContainerItem_Loaded(object sender, RoutedEventArgs e)
        {
            OnItemsSourceChanged();
        }

        private static void OnRadioMenuFlyoutItemChecked(DependencyObject sender, DependencyProperty dp)
        {
            var item = (RadioMenuFlyoutItem)sender;
            if (item.IsChecked
                && item.GetValue(FakeParentProperty) is RadioMenuFlyoutItemsContainerItem container
                && !container.isSourceChanging)
            {
                var selectedItem = item.DataContext;
                container.SelectedItem = selectedItem;

                var commandParameter = container.SelectedCommandParameter ?? selectedItem;
                if (container.SelectedCommand?.CanExecute(commandParameter) == true)
                {
                    container.SelectedCommand.Execute(commandParameter);
                }
            }
        }
    }
}

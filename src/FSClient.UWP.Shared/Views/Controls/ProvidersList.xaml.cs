namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using Windows.Foundation.Metadata;
    using Windows.System;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
#endif
    using FSClient.UWP.Shared.Helpers;
    using FSClient.ViewModels.Items;

    public sealed partial class ProvidersList : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IList<ProviderViewModel>), typeof(ProvidersList),
                PropertyMetadata.Create(() => new List<ProviderViewModel>()));

        public ProvidersList()
        {
            InitializeComponent();
        }

        public IList<ProviderViewModel>? ItemsSource
        {
            get => (IList<ProviderViewModel>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private async void ProvidersListView_ChoosingItemContainer(ListViewBase sender,
            ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer is not ListViewItem container)
            {
                args.ItemContainer = container = new ListViewItem {ContentTemplate = sender.ItemTemplate};

                await container.WaitForLoadedAsync().ConfigureAwait(true);

                var reorderControl = container.FindVisualChild<Control>("ReorderControl")
                                     ?? throw new InvalidOperationException("ReorderControl is missed.");
                container.Tag = reorderControl;

                if (ApiInformation.IsEventPresent(typeof(UIElement).FullName, nameof(PreviewKeyDown)))
                {
                    reorderControl.KeyDown -= ReorderControl_KeyDown;
                    reorderControl.PreviewKeyDown += ReorderControl_KeyDown;
                }

                if (ApiInformation.IsPropertyPresent(typeof(Control).FullName, nameof(UseSystemFocusVisuals)))
                {
                    reorderControl.UseSystemFocusVisuals = true;
                }

                container.GotFocus += ItemContainer_GotFocus;
                container.LostFocus += ItemContainer_LostFocus;
            }
        }

        private void ItemContainer_LostFocus(object sender, object _)
        {
            var control = (Control)sender;
            var newFocusedElement = FocusManager.GetFocusedElement() as DependencyObject;
            if (newFocusedElement != sender
                && newFocusedElement?.IsChildOf(control) == false)
            {
                if (control.Tag is not Control reorderControl)
                {
                    return;
                }

                reorderControl.Visibility = Visibility.Collapsed;
            }
        }

        private void ItemContainer_GotFocus(object sender, RoutedEventArgs args)
        {
            if (args.OriginalSource is Control source
                && source.FocusState != FocusState.Pointer
                && source.FocusState != FocusState.Programmatic
                && ((FrameworkElement)sender).Tag is Control reorderControl
                && reorderControl.Visibility != Visibility.Visible)
            {
                reorderControl.Visibility = Visibility.Visible;
                reorderControl.UseSystemFocusVisuals = true;
            }
        }

        private void ReorderControl_KeyDown(object sender, KeyRoutedEventArgs args)
        {
            var item = (sender as FrameworkElement)?.FindVisualAscendant<ListViewItem>();
            if (item == null)
            {
                return;
            }

            var oldIndex = ProvidersListView.IndexFromContainer(item);
            var newIndex = oldIndex;
            switch (args.Key)
            {
                case VirtualKey.Up:
                case VirtualKey.GamepadDPadUp:
                    newIndex--;
                    break;
                case VirtualKey.Down:
                case VirtualKey.GamepadDPadDown:
                    newIndex++;
                    break;
                default:
                    return;
            }

            if (oldIndex < 0
                || newIndex < 0
                || newIndex > ProvidersListView.Items.Count)
            {
                return;
            }

            if (ItemsSource is ObservableCollection<ProviderViewModel> observableCollection)
            {
                observableCollection.Move(oldIndex, newIndex);
            }
            else if (ItemsSource != null)
            {
                var oldItem = ItemsSource[oldIndex];
                ItemsSource.RemoveAt(oldIndex);
                ItemsSource.Insert(newIndex, oldItem);
            }

            args.Handled = true;
        }
    }
}

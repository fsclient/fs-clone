namespace FSClient.UWP.Shared.Views.Controls
{
    using System.ComponentModel;
    using System.Windows.Input;

    using Windows.Foundation.Metadata;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Models;
    using FSClient.UWP.Shared.Helpers;

    public sealed class NodeProgressControl : Control
    {
        private bool isPositionChangingByControl;

        private readonly bool isGettingFocusAvailable
            = ApiInformation.IsEventPresent(typeof(UIElement).FullName, nameof(GettingFocus));

        public static readonly DependencyProperty NodeProperty =
            DependencyProperty.Register(nameof(Node), typeof(ITreeNode), typeof(NodeProgressControl),
                new PropertyMetadata(null, NodeChanged));

        private static void NodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var nodeControl = (NodeProgressControl)d;
            if (e.OldValue is INotifyPropertyChanged oldNode)
            {
                oldNode.PropertyChanged -= nodeControl.Node_PropertyChanged;
            }

            if (e.NewValue is INotifyPropertyChanged newNode)
            {
                newNode.PropertyChanged += nodeControl.Node_PropertyChanged;
            }

            nodeControl.IsEnabled = e.NewValue != null;
        }

        public static readonly DependencyProperty SaveNodePositionCommandProperty =
            DependencyProperty.Register(nameof(SaveNodePositionCommand), typeof(ICommand), typeof(NodeProgressControl),
                new PropertyMetadata(null));

        private ToggleSwitch? isWatchedToggleSwitch;
        private ExtendedSlider? positionSlider;

        public NodeProgressControl()
        {
            DefaultStyleKey = typeof(NodeProgressControl);

            if (isGettingFocusAvailable)
            {
                GettingFocus += NodeProgressControl_GettingFocus;
            }
        }

        private void NodeProgressControl_GettingFocus(UIElement sender, GettingFocusEventArgs args)
        {
            if (GetTemplateChild("RootGrid") is Grid rootGrid
                && !args.OldFocusedElement.IsChildOf(this))
            {
                args.Handled = args.TrySetNewFocusedElement(rootGrid);
            }
        }

        public ICommand? SaveNodePositionCommand
        {
            get => (ICommand?)GetValue(SaveNodePositionCommandProperty);
            set => SetValue(SaveNodePositionCommandProperty, value);
        }

        public ITreeNode? Node
        {
            get => (ITreeNode?)GetValue(NodeProperty);
            set => SetValue(NodeProperty, value);
        }

        protected override void OnApplyTemplate()
        {
            if (GetTemplateChild("ShowSliderButton") is AppBarToggleButton showSliderButton)
            {
                showSliderButton.Checked += ShowSliderButton_Checked;
            }

            EnsureIsWatchedToggleSwitch();
            EnsurePositionSlider();

            base.OnApplyTemplate();
        }

        private void EnsureIsWatchedToggleSwitch()
        {
            if (Node != null
                && GetTemplateChild("IsWatchedToggleSwitch") is ToggleSwitch isWatchedToggleSwitch)
            {
                isWatchedToggleSwitch.IsOn = Node.IsWatched;
                isWatchedToggleSwitch.Toggled += IsWatchedToggleSwitch_Toggled;
                this.isWatchedToggleSwitch = isWatchedToggleSwitch;
            }
        }

        private void EnsurePositionSlider()
        {
            if (Node != null
                && GetTemplateChild("PositionSlider") is ExtendedSlider positionSlider)
            {
                positionSlider.Value = Node.Position;
                positionSlider.ValueChangeCompleted += PositionSlider_ValueChangeCompleted;
                this.positionSlider = positionSlider;
            }
        }

        private void ShowSliderButton_Checked(object sender, RoutedEventArgs e)
        {
            ((AppBarToggleButton)sender).Checked -= ShowSliderButton_Checked;

            if ((GetTemplateChild("ProgressGrid") ?? FindName("ProgressGrid")) is Grid proggresGrid)
            {
                EnsurePositionSlider();
                proggresGrid.Visibility = Visibility.Visible;
            }

            if ((GetTemplateChild("SwitchGrid") ?? FindName("SwitchGrid")) is Grid switchGrid)
            {
                switchGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void Node_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var node = (ITreeNode)sender;
            if (e.PropertyName == nameof(ITreeNode.IsWatched)
                && isWatchedToggleSwitch != null)
            {
                isWatchedToggleSwitch.IsOn = node.IsWatched;
            }

            if (e.PropertyName == nameof(ITreeNode.Position)
                && positionSlider != null)
            {
                positionSlider.Value = node.Position;
            }
        }

        private void IsWatchedToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (isPositionChangingByControl)
            {
                return;
            }

            isPositionChangingByControl = true;
            try
            {
                if (Node != null
                    && isWatchedToggleSwitch != null
                    && this.IsLoaded())
                {
                    Node.IsWatched = isWatchedToggleSwitch.IsOn;
                    SaveNodePositionCommand?.Execute(Node);
                }
            }
            finally
            {
                isPositionChangingByControl = false;
            }
        }

        private void PositionSlider_ValueChangeCompleted(object sender, SliderValueChangeCompletedEventArgs args)
        {
            if (isPositionChangingByControl)
            {
                return;
            }

            isPositionChangingByControl = true;
            try
            {
                if (Node != null
                    && this.IsLoaded())
                {
                    Node.Position = (float)args.NewValue;
                    SaveNodePositionCommand?.Execute(Node);
                }
            }
            finally
            {
                isPositionChangingByControl = false;
            }
        }
    }
}

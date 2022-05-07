namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Threading.Tasks;

    using Windows.Foundation.Metadata;
    using Windows.System;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Input;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
    using Windows.UI.Xaml.Input;
#endif

    using FSClient.UWP.Shared.Helpers;
    using FSClient.Localization.Resources;

    public class HideableTextBox : TextBox
    {
        public static readonly DependencyProperty ShowIconProperty =
            DependencyProperty.Register(nameof(ShowIcon), typeof(Symbol), typeof(HideableTextBox),
                new PropertyMetadata(Symbol.Find));

        public static readonly DependencyProperty IsTextBoxVisibleProperty =
            DependencyProperty.Register(nameof(IsTextBoxVisible), typeof(bool), typeof(HideableTextBox),
                new PropertyMetadata(false));

        public HideableTextBox()
        {
            DefaultStyleKey = typeof(HideableTextBox);

            Loaded += SearchBoxControl_Loaded;
            if (ApiInformation.IsEventPresent(typeof(UIElement).FullName, nameof(PreviewKeyDown)))
            {
                PreviewKeyDown += HideableTextBox_PreviewKeyDown;
            }

            if (ApiInformation.IsEventPresent(typeof(UIElement).FullName, nameof(GettingFocus)))
            {
                GettingFocus += HideableTextBox_GettingFocus;
            }
        }

        public Symbol ShowIcon
        {
            get => (Symbol)GetValue(ShowIconProperty);
            set => SetValue(ShowIconProperty, value);
        }

        public bool IsTextBoxVisible
        {
            get => (bool)GetValue(IsTextBoxVisibleProperty);
            private set => SetValue(IsTextBoxVisibleProperty, value);
        }

        protected override void OnApplyTemplate()
        {
            if (GetTemplateChild("ShowTextBoxButton") is ButtonBase showTextBoxButton)
            {
                showTextBoxButton.Click += ShowTextBoxButton_Click;

                if (ApiInformation.IsTypePresent(typeof(KeyboardAccelerator).FullName))
                {
                    var key = new KeyboardAccelerator() {Key = VirtualKey.F, Modifiers = VirtualKeyModifiers.Control};
                    key.Invoked += (_, __) => ShowTextBox();
                    showTextBoxButton.KeyboardAccelerators.Add(key);
                    ToolTipService.SetToolTip(showTextBoxButton, Strings.HideableTextBox_FolderSearch + " (Ctrl+F)");
                }
            }

            base.OnApplyTemplate();
        }

        private void HideableTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape
                && IsTextBoxVisible)
            {
                e.Handled = HideTextBox();
            }
        }

        private void HideableTextBox_GettingFocus(UIElement sender, GettingFocusEventArgs args)
        {
            if (args.InputDevice == FocusInputDeviceKind.Keyboard
                || args.InputDevice == FocusInputDeviceKind.GameController)
            {
                if (!IsTextBoxVisible
                    && GetTemplateChild("ShowTextBoxButton") is ButtonBase showTextBoxButton
                    && args.TrySetNewFocusedElement(showTextBoxButton))
                {
                    LostFocus -= OnLostFocus;
                    LostFocus += OnLostFocus;
                }
            }
        }

        private async void SearchBoxControl_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SearchBoxControl_Loaded;

            await Task.Yield();
            HideTextBox();
        }

        private void ShowTextBoxButton_Click(object sender, RoutedEventArgs e)
        {
            ShowTextBox();
        }

        protected async void OnLostFocus(object sender, RoutedEventArgs e)
        {
            await Task.Yield();
            if (IsTextBoxVisible
                && e.OriginalSource is FrameworkElement originalSource
                && !originalSource.IsChildOf(this))
            {
                HideTextBox();
            }
        }

        private async void ShowTextBox()
        {
            if (VisualStateManager.GoToState(this, "TextBoxVisible", true))
            {
                Width = double.NaN;
                IsTextBoxVisible = true;
                await Task.Yield();
                LostFocus -= OnLostFocus;
                LostFocus += OnLostFocus;
                Focus(FocusState.Programmatic);
            }
        }

        private bool HideTextBox()
        {
            if (VisualStateManager.GoToState(this, "TextBoxInvisible", true)
                && GetTemplateChild("ShowTextBoxButton") is ButtonBase showTextBoxButton)
            {
                LostFocus -= OnLostFocus;
                Width = showTextBoxButton.Width;
                IsTextBoxVisible = false;
                return true;
            }

            return false;
        }
    }
}

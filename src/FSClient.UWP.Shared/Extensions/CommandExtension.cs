namespace FSClient.UWP.Shared.Extensions
{
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Views.Controls;
    public static class CommandExtension
    {
        #region CommandParameter

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.RegisterAttached("CommandParameter", typeof(object), typeof(CommandExtension),
                new PropertyMetadata(null));

        public static object? GetCommandParameter(DependencyObject d)
        {
            return d.GetValue(CommandParameterProperty);
        }

        public static void SetCommandParameter(DependencyObject d, object? value)
        {
            d.SetValue(CommandParameterProperty, value);
        }

        #endregion

        #region List/Selector.ItemClickCommand

        public static readonly DependencyProperty ClickCommandProperty =
            DependencyProperty.RegisterAttached("ClickCommand", typeof(ICommand), typeof(CommandExtension),
                new PropertyMetadata(null, OnClickCommandPropertyChanged));

        public static ICommand? GetClickCommand(DependencyObject d)
        {
            return d.GetValue(ClickCommandProperty) as ICommand;
        }

        public static void SetClickCommand(DependencyObject d, ICommand? value)
        {
            d.SetValue(ClickCommandProperty, value);
        }

        private static void OnClickCommandPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            switch (d)
            {
                case ListViewBase listBase when listBase.IsItemClickEnabled:
                    listBase.ItemClick -= OnItemClick;
                    if (e.NewValue != null)
                    {
                        listBase.ItemClick += OnItemClick;
                    }

                    return;
                case Selector selectorBase:
                    selectorBase.SelectionChanged -= OnSelectionChanged;
                    if (e.NewValue != null)
                    {
                        selectorBase.SelectionChanged += OnSelectionChanged;
                    }

                    return;
                case Pivot pivot:
                    pivot.SelectionChanged -= Pivot_SelectionChanged;
                    if (e.NewValue != null)
                    {
                        pivot.SelectionChanged += Pivot_SelectionChanged;
                    }

                    return;
            }
        }

        private static void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var pivot = (Pivot)sender;
            if (GetCommandParameter(pivot) is object parameter)
            {
                GetClickCommand(pivot)?.Execute(parameter);
            }
            else
            {
                var addedItems = e.AddedItems.Except(e.RemovedItems);

                foreach (var item in addedItems)
                {
                    GetClickCommand(pivot)?.Execute(item);
                }
            }
        }

        private static async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            await Task.Yield();

            var list = (ListViewBase)sender;
            GetClickCommand(list)?.Execute(GetCommandParameter(list) ?? e.ClickedItem);
        }

        private static async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await Task.Yield();

            var selector = (Selector)sender;

            if (GetCommandParameter(selector) is object parameter)
            {
                GetClickCommand(selector)?.Execute(parameter);
            }
            else
            {
                var addedItems = e.AddedItems.Except(e.RemovedItems);

                foreach (var item in addedItems)
                {
                    GetClickCommand(selector)?.Execute(item);
                }
            }
        }

        #endregion

        #region TextBox/RichEditBox/AutoSuggestBox.TextChangedCommand

        public static readonly DependencyProperty TextChangedCommandProperty =
            DependencyProperty.RegisterAttached("TextChangedCommand", typeof(ICommand), typeof(CommandExtension),
                new PropertyMetadata(null, OnTextChangedCommandChanged));

        public static ICommand? GetTextChangedCommand(UIElement element)
        {
            return element.GetValue(TextChangedCommandProperty) as ICommand;
        }

        public static void SetTextChangedCommand(UIElement element, ICommand? value)
        {
            element.SetValue(TextChangedCommandProperty, value);
        }

        private static void OnTextChangedCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            switch (d)
            {
                case TextBox textBox:
                    textBox.TextChanged -= TextChanged;
                    if (e.NewValue != null)
                    {
                        textBox.TextChanged += TextChanged;
                    }

                    break;
                case RichEditBox editBox:
                    editBox.TextChanged -= TextChanged;
                    if (e.NewValue != null)
                    {
                        editBox.TextChanged += TextChanged;
                    }

                    break;
                case AutoSuggestBox suggestBox:
                    suggestBox.TextChanged -= TextChanged;
                    if (e.NewValue != null)
                    {
                        suggestBox.TextChanged += TextChanged;
                    }

                    break;
            }
        }

        private static async void TextChanged(object sender, object e)
        {
            if (e is AutoSuggestBoxTextChangedEventArgs autoSuggestBoxTextChangedEventArgs
                && autoSuggestBoxTextChangedEventArgs.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            await Task.Yield();

            var element = (FrameworkElement)sender;
            if (!element.IsLoaded())
            {
                return;
            }

            var command = GetTextChangedCommand(element);
            var commandParameter = GetCommandParameter(element);

            if (command?.CanExecute(commandParameter) != true)
            {
                return;
            }

            command.Execute(commandParameter);
        }

        #endregion

        #region RangeBase.ValueChangedCommand

        public static readonly DependencyProperty ValueChangedCommandProperty =
            DependencyProperty.RegisterAttached("ValueChangedCommand", typeof(ICommand), typeof(CommandExtension),
                new PropertyMetadata(null, OnValueChangedCommandChanged));

        public static ICommand? GetValueChangedCommand(UIElement element)
        {
            return element.GetValue(ValueChangedCommandProperty) as ICommand;
        }

        public static void SetValueChangedCommand(UIElement element, ICommand? value)
        {
            element.SetValue(ValueChangedCommandProperty, value);
        }

        private static void OnValueChangedCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            switch (d)
            {
                case ExtendedSlider slider:
                    slider.ValueChangeCompleted -= ValueChangeCompleted;
                    if (e.NewValue != null)
                    {
                        slider.ValueChangeCompleted += ValueChangeCompleted;
                    }

                    break;
                case RangeBase rangeBase:
                    rangeBase.ValueChanged -= ValueChanged;
                    if (e.NewValue != null)
                    {
                        rangeBase.ValueChanged += ValueChanged;
                    }

                    break;
            }
        }

        private static async void ValueChangeCompleted(object sender, SliderValueChangeCompletedEventArgs e)
        {
            await Task.Yield();

            var element = (FrameworkElement)sender;
            if (!element.IsLoaded())
            {
                return;
            }

            var command = GetValueChangedCommand(element);
            var commandParameter = GetCommandParameter(element) ?? e.NewValue;

            if (command?.CanExecute(commandParameter) != true)
            {
                return;
            }

            command.Execute(commandParameter);
        }

        private static async void ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            await Task.Yield();

            var element = (FrameworkElement)sender;
            if (!element.IsLoaded())
            {
                return;
            }

            var command = GetValueChangedCommand(element);
            var commandParameter = GetCommandParameter(element) ?? e.NewValue;

            if (command?.CanExecute(commandParameter) != true)
            {
                return;
            }

            command.Execute(commandParameter);
        }

        #endregion

        #region ToggleSwitch/ToggleButton.ToggledCommand

        public static readonly DependencyProperty ToggledCommandProperty =
            DependencyProperty.RegisterAttached("ToggledCommand", typeof(ICommand), typeof(CommandExtension),
                new PropertyMetadata(null, OnToggledCommandChanged));

        public static ICommand? GetToggledCommand(UIElement element)
        {
            return element.GetValue(ToggledCommandProperty) as ICommand;
        }

        public static void SetToggledCommand(UIElement element, ICommand? value)
        {
            element.SetValue(ToggledCommandProperty, value);
        }

        private static void OnToggledCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            switch (d)
            {
                case ToggleSwitch toggleSwitch:
                    toggleSwitch.Toggled -= ToggleSwith_Toggled;
                    if (e.NewValue != null)
                    {
                        toggleSwitch.Toggled += ToggleSwith_Toggled;
                    }

                    break;
                case ToggleButton toggleButton:
                    toggleButton.Checked -= ToggleButton_Checked;
                    toggleButton.Unchecked -= ToggleButton_Checked;
                    toggleButton.Indeterminate -= ToggleButton_Checked;
                    if (e.NewValue != null)
                    {
                        toggleButton.Checked += ToggleButton_Checked;
                        toggleButton.Unchecked += ToggleButton_Checked;
                        toggleButton.Indeterminate += ToggleButton_Checked;
                    }

                    break;
            }
        }

        private static async void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            await Task.Yield();

            var element = (ToggleButton)sender;
            if (!element.IsLoaded())
            {
                return;
            }

            var command = GetToggledCommand(element);
            var commandParameter = GetCommandParameter(element) ?? element.IsChecked;

            if (command?.CanExecute(commandParameter) != true)
            {
                return;
            }

            command.Execute(commandParameter);
        }

        private static async void ToggleSwith_Toggled(object sender, RoutedEventArgs e)
        {
            await Task.Yield();

            var element = (ToggleSwitch)sender;
            if (!element.IsLoaded())
            {
                return;
            }

            var command = GetToggledCommand(element);
            var commandParameter = GetCommandParameter(element) ?? element.IsOn;

            if (command?.CanExecute(commandParameter) != true)
            {
                return;
            }

            command.Execute(commandParameter);
        }

        #endregion
    }
}

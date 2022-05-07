namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.Localization.Resources;
    using Humanizer;

    public sealed partial class YearSelector : UserControl
    {
        public YearSelector()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        public event EventHandler<Range?>? YearSelected;

        public ICommand? YearSelectedCommand { get; set; }

        public object? YearSelectedCommandParameter { get; set; }

        public static readonly DependencyProperty AllowYearsRangeProperty =
            DependencyProperty.Register(nameof(AllowYearsRange), typeof(bool), typeof(YearSelector),
                new PropertyMetadata(false, InitControls));

        public bool AllowYearsRange
        {
            get => (bool)GetValue(AllowYearsRangeProperty);
            set => SetValue(AllowYearsRangeProperty, value);
        }

        public static readonly DependencyProperty YearLimitProperty =
            DependencyProperty.Register(nameof(YearLimit), typeof(Range), typeof(YearSelector),
                new PropertyMetadata(new Range(1900, DateTime.Now.Year + 1), InitControls));

        public Range YearLimit
        {
            get => (Range)GetValue(YearLimitProperty);
            set => SetValue(YearLimitProperty, value);
        }

        public static readonly DependencyProperty SelectedYearProperty =
            DependencyProperty.Register(nameof(SelectedYear), typeof(Range?), typeof(YearSelector),
                new PropertyMetadata(null, SelectedYearValueChanged));

        public Range? SelectedYear
        {
            get => GetValue(SelectedYearProperty) as Range?;
            set => SetValue(SelectedYearProperty, value);
        }

        private static void InitControls(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                var control = (YearSelector)d;
                if ((bool)d.GetValue(AllowYearsRangeProperty))
                {
                    if (control.FromYearUpDown == null)
                    {
                        control.FindName(nameof(FromYearUpDown));
                    }
                    else
                    {
                        control.InitFromRangeYearSelectorIfExist();
                    }

                    if (control.ToYearUpDown == null)
                    {
                        control.FindName(nameof(ToYearUpDown));
                    }
                    else
                    {
                        control.InitToRangeYearSelectorIfExist();
                    }
                }
                else
                {
                    if (control.SingleYearSelector == null)
                    {
                        control.FindName(nameof(SingleYearSelector));
                    }
                    else
                    {
                        control.InitSingleYearSelectorIfExist();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void InitSingleYearSelectorIfExist()
        {
            try
            {
                if (SingleYearSelector == null)
                {
                    return;
                }

                SingleYearSelector.SelectionChanged -= SingleYearSelector_SelectionChanged;

                SingleYearSelector.Items.Clear();

                SingleYearSelector.Items.Add(new ComboBoxItem
                {
                    Content = Strings.YearSelector_AllYears,
                    Tag = null,
                    IsSelected = !SelectedYear.HasValue
                });

                for (var year = YearLimit.End.Value; year > YearLimit.Start.Value; year--)
                {
                    SingleYearSelector.Items.Add(new ComboBoxItem
                    {
                        Content = Strings.YearSelector_YearFormat.FormatWith(year),
                        Tag = year,
                        IsSelected = year == SelectedYear?.Start.Value
                    });
                }

                SingleYearSelector.SelectionChanged += SingleYearSelector_SelectionChanged;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void InitFromRangeYearSelectorIfExist()
        {
            try
            {
                if (FromYearUpDown == null)
                {
                    return;
                }

                FromYearUpDown.DateChanged -= FromYearUpDown_DateChanged;

                FromYearUpDown.MinYear = new DateTimeOffset(new DateTime(YearLimit.Start.Value, 1, 1));
                if (SelectedYear.HasValue)
                {
                    FromYearUpDown.Date = new DateTimeOffset(new DateTime(SelectedYear.Value.Start.Value, 1, 1));
                    FromYearUpDown.MaxYear = new DateTimeOffset(new DateTime(SelectedYear.Value.End.Value, 1, 1));
                }
                else
                {
                    FromYearUpDown.Date = FromYearUpDown.MinYear;
                    FromYearUpDown.MaxYear = new DateTimeOffset(new DateTime(YearLimit.End.Value - 1, 1, 1));
                }

                FromYearUpDown.DateChanged += FromYearUpDown_DateChanged;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void InitToRangeYearSelectorIfExist()
        {
            try
            {
                if (ToYearUpDown == null)
                {
                    return;
                }

                ToYearUpDown.DateChanged -= ToYearUpDown_DateChanged;

                ToYearUpDown.MaxYear = new DateTimeOffset(new DateTime(YearLimit.End.Value, 1, 1));
                if (SelectedYear.HasValue)
                {
                    ToYearUpDown.Date = new DateTimeOffset(new DateTime(SelectedYear.Value.End.Value, 1, 1));
                    ToYearUpDown.MinYear = new DateTimeOffset(new DateTime(SelectedYear.Value.Start.Value, 1, 1));
                }
                else
                {
                    ToYearUpDown.Date = ToYearUpDown.MaxYear;
                    ToYearUpDown.MinYear = new DateTimeOffset(new DateTime(YearLimit.Start.Value, 1, 1));
                }

                ToYearUpDown.DateChanged += ToYearUpDown_DateChanged;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private static void SelectedYearValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                var control = (YearSelector)d;

                if (control.AllowYearsRange)
                {
                    if (control.FromYearUpDown == null)
                    {
                        control.FindName(nameof(FromYearUpDown));
                    }
                    else
                    {
                        control.InitFromRangeYearSelectorIfExist();
                    }

                    if (control.ToYearUpDown == null)
                    {
                        control.FindName(nameof(ToYearUpDown));
                    }
                    else
                    {
                        control.InitToRangeYearSelectorIfExist();
                    }
                }
                else
                {
                    if (control.SingleYearSelector == null)
                    {
                        control.FindName(nameof(SingleYearSelector));
                        return;
                    }

                    if (control.SingleYearSelector.Items.Count == 0)
                    {
                        control.InitSingleYearSelectorIfExist();
                    }
                    else
                    {
                        control.SingleYearSelector.SelectionChanged -= control.SingleYearSelector_SelectionChanged;

                        control.SingleYearSelector.SelectedItem = control
                            .SingleYearSelector
                            .Items
                            .OfType<ComboBoxItem>()
                            .FirstOrDefault(i => (i.Tag as int?) == (e.NewValue as Range?)?.Start.Value);

                        control.SingleYearSelector.SelectionChanged += control.SingleYearSelector_SelectionChanged;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void SingleYearSelector_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitSingleYearSelectorIfExist();
                SingleYearSelector.Loaded -= SingleYearSelector_Loaded;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void ToYearUpDown_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitToRangeYearSelectorIfExist();
                FromYearUpDown.Loaded -= ToYearUpDown_Loaded;
                InitWidthDatePicket(ToYearUpDown);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void FromYearUpDown_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitFromRangeYearSelectorIfExist();
                FromYearUpDown.Loaded -= FromYearUpDown_Loaded;
                InitScrollDownDateTimePickerOnOpen(FromYearUpDown);
                InitWidthDatePicket(FromYearUpDown);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void InitWidthDatePicket(DatePicker datePicker)
        {
            var buttonContentGrid = datePicker.FindVisualChild<ButtonBase>("FlyoutButton");
            if (buttonContentGrid != null)
            {
                buttonContentGrid.MinWidth = 50;
            }
        }

        private void InitScrollDownDateTimePickerOnOpen(DatePicker datePicker)
        {
            var pickerButton = datePicker.FindVisualChildren<ButtonBase>().FirstOrDefault();

            if (pickerButton != null)
            {
                pickerButton.Click += async (s, a) =>
                {
                    // Wait 'till it opens flyout
                    // Need better way to do it
                    // We can't access the DatePickerFlyout to handle Opened event
                    await Task.Delay(50);

                    var scroll = VisualTreeHelper
                        .GetOpenPopups(Window.Current)
                        .Select(f => f.Child)
                        .OfType<DatePickerFlyoutPresenter>()
                        .OfType<DependencyObject>()
                        .FirstOrDefault()?
                        .FindVisualChildren<ScrollViewer>()
                        .FirstOrDefault();

                    scroll?.ChangeView(scroll.HorizontalOffset, scroll.ScrollableHeight, scroll.ZoomFactor, true);
                };
            }
        }

        private void SingleYearSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.AddedItems.FirstOrDefault() is not ComboBoxItem item)
                {
                    return;
                }

                switch (item.Tag)
                {
                    case int year:
                        SelectedYear = new Range(year, year + 1);
                        OnYearSelected();
                        break;
                    case null:
                        SelectedYear = null;
                        OnYearSelected();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void FromYearUpDown_DateChanged(object sender, DatePickerValueChangedEventArgs e)
        {
            try
            {
                if (e.NewDate.Year != e.OldDate.Year)
                {
                    SelectedYear = new Range(FromYearUpDown.Date.Year, ToYearUpDown.Date.Year + 1);
                    ToYearUpDown.MinYear = e.NewDate;

                    OnYearSelected();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void ToYearUpDown_DateChanged(object sender, DatePickerValueChangedEventArgs e)
        {
            try
            {
                if (e.NewDate.Year != e.OldDate.Year)
                {
                    SelectedYear = new Range(FromYearUpDown.Date.Year, ToYearUpDown.Date.Year + 1);
                    FromYearUpDown.MaxYear = e.NewDate;

                    OnYearSelected();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void OnYearSelected()
        {
            try
            {
                YearSelected?.Invoke(this, SelectedYear);
                var parameter = YearSelectedCommandParameter ?? SelectedYear;
                if (YearSelectedCommand?.CanExecute(parameter) ?? false)
                {
                    YearSelectedCommand.Execute(parameter);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }
    }
}

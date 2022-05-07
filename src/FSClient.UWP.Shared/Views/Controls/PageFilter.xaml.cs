namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.ViewModels.Abstract;

    public sealed partial class PageFilter : UserControl
    {
        public static readonly DependencyProperty FilterProperty =
            DependencyProperty.Register(nameof(Filter), typeof(ISectionPageViewModel<SectionPageParams>), typeof(PageFilter),
                new PropertyMetadata(null, FilterChanged));

        private static async void FilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            await Task.Yield();
            ((PageFilter)d).filterChanging = false;
        }

        private bool tagsSelectionChanging;
        private bool sortSelectionChanging;
        private bool filterChanging;

        public PageFilter()
        {
            InitializeComponent();
        }

        public ICommand? SubmitCommand { get; set; }

        public ISectionPageViewModel<SectionPageParams> Filter
        {
            get => (ISectionPageViewModel<SectionPageParams>)GetValue(FilterProperty);
            set
            {
                filterChanging = true;
                SetValue(FilterProperty, value);
            }
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox senderComboBox
                || sortSelectionChanging
                || filterChanging
                || !senderComboBox.IsLoaded()
                || e.AddedItems.SequenceEqual(e.RemovedItems))
            {
                return;
            }

            SubmitCommand?.Execute(false);
        }

        private void SortComboBox_Loaded(object sender, RoutedEventArgs _)
        {
            var senderComboBox = (ComboBox)sender;

            if (senderComboBox?.Tag is not ISectionPageViewModel<SectionPageParams> secPage)
            {
                return;
            }

            sortSelectionChanging = true;

            if (senderComboBox.SelectedIndex == -1
                || senderComboBox.SelectedItem is not SortType sortType
                || !secPage.PageParams.SortTypes.Contains(sortType))
            {
                senderComboBox.SelectedIndex = 0;
            }

            sortSelectionChanging = false;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox senderComboBox
                || tagsSelectionChanging
                || filterChanging
                || !senderComboBox.IsLoaded()
                || e.AddedItems.SequenceEqual(e.RemovedItems))
            {
                return;
            }

            var itemsControl = senderComboBox.FindVisualAscendant<ItemsControl>();

            if (itemsControl?.Tag is not ISectionPageViewModel<SectionPageParams> secPage)
            {
                return;
            }

            tagsSelectionChanging = true;

            if (!secPage.PageParams.AllowMultiTag)
            {
                foreach (var box in itemsControl.FindVisualChildren<ComboBox>())
                {
                    if (box != senderComboBox
                        && box.SelectedItem is TitledTag tag
                        && secPage.SelectedTags.Contains(tag))
                    {
                        secPage.SelectedTags.Remove(tag);
                        box.SelectedIndex = 0;
                    }
                }

                secPage.Year = null;
            }

            var changed = false;
            foreach (var added in e.AddedItems.OfType<TitledTag>())
            {
                if (added != TitledTag.Any
                    && !secPage.SelectedTags.Contains(added))
                {
                    secPage.SelectedTags.Add(added);
                    changed = true;
                }
            }

            foreach (var removed in e.RemovedItems.OfType<TitledTag>())
            {
                if (secPage.SelectedTags.Contains(removed))
                {
                    secPage.SelectedTags.Remove(removed);
                    changed = true;
                }
            }

            tagsSelectionChanging = false;

            if (changed)
            {
                SubmitCommand?.Execute(false);
            }
        }

        private void ComboBox_Loaded(object sender, RoutedEventArgs _)
        {
            var senderComboBox = (ComboBox)sender;
            var itemsControl = senderComboBox.FindVisualAscendant<ItemsControl>();

            if (itemsControl?.Tag is not ISectionPageViewModel<SectionPageParams> secPage)
            {
                return;
            }

            tagsSelectionChanging = true;

            if (senderComboBox.SelectedIndex == -1
                || senderComboBox.SelectedItem is not TitledTag tag
                || !secPage.SelectedTags.Contains(tag))
            {
                senderComboBox.SelectedIndex = 0;
            }

            if (senderComboBox.SelectedIndex == 0
                && secPage.SelectedTags.FirstOrDefault(t => senderComboBox.Items.OfType<TitledTag>().Any(i => i == t))
                    is var missedTag
                && missedTag != TitledTag.Any)
            {
                senderComboBox.SelectedItem = missedTag;
            }

            tagsSelectionChanging = false;
        }
    }
}

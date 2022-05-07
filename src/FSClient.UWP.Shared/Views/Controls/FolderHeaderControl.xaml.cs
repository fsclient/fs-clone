namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Windows.Input;

    using Windows.Foundation;
    using Windows.Foundation.Metadata;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Models;

    public sealed partial class FolderHeaderControl : UserControl
    {
        public static readonly DependencyProperty FolderProperty =
            DependencyProperty.Register(nameof(Folder), typeof(IFolderTreeNode), typeof(FolderHeaderControl),
                new PropertyMetadata(null, FolderChanged));

        public static readonly DependencyProperty ShowProvidersPaneProperty =
            DependencyProperty.Register(nameof(ShowProvidersPane), typeof(bool), typeof(FolderHeaderControl),
                new PropertyMetadata(false));

        public FolderHeaderControl()
        {
            InitializeComponent();
        }

        public IFolderTreeNode? Folder
        {
            get => (IFolderTreeNode?)GetValue(FolderProperty);
            set => SetValue(FolderProperty, value);
        }

        public bool ShowProvidersPane
        {
            get => (bool)GetValue(ShowProvidersPaneProperty);
            set => SetValue(ShowProvidersPaneProperty, value);
        }

        public ICommand? SearchInFolderCommand { get; set; }

        private static void FolderChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        {
            if (((FolderHeaderControl)d).SearchTextBox is HideableTextBox hideableTextBox)
            {
                hideableTextBox.Text = string.Empty;
            }
        }

        private void SearchTextBox_TextChanged(object _, TextChangedEventArgs __)
        {
            if (SearchInFolderCommand?.CanExecute(SearchTextBox.Text) == true)
            {
                SearchInFolderCommand.Execute(SearchTextBox.Text);
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ApiInformation.IsApiContractPresent(typeof(UniversalApiContract).FullName, 5)
                && FindName("SearchTextBox") is HideableTextBox hideableTextBox)
            {
                hideableTextBox.Visibility = Visibility.Visible;
            }
        }
    }
}

namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Windows.Input;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.Shared.Models;

    public partial class MediaHeader
    {
        public File? CurrentFile
        {
            get => GetValue(CurrentFileProperty) as File;
            set => SetValue(CurrentFileProperty, value);
        }

        public static readonly DependencyProperty CurrentFileProperty =
            DependencyProperty.Register(nameof(CurrentFile), typeof(int), typeof(MediaHeader),
                new PropertyMetadata(null, CurrentFileChanged));

        public bool IsPlaylistOpen
        {
            get => (bool)GetValue(IsPlaylistOpenProperty);
            set => SetValue(IsPlaylistOpenProperty, value);
        }

        public static readonly DependencyProperty IsPlaylistOpenProperty =
            DependencyProperty.Register(nameof(IsPlaylistOpen), typeof(bool), typeof(MediaHeader),
                new PropertyMetadata(false));

        public FlyoutBase? MoreFlyout
        {
            get => (FlyoutBase?)GetValue(MoreFlyoutProperty);
            set => SetValue(MoreFlyoutProperty, value);
        }

        public static readonly DependencyProperty MoreFlyoutProperty =
            DependencyProperty.Register(nameof(MoreFlyout), typeof(FlyoutBase), typeof(MediaHeader),
                new PropertyMetadata(null, MoreFlyoutChanged));

        public ICommand? GoNextCommand { get; set; }

        public ICommand? GoPreviousCommand { get; set; }
    }
}

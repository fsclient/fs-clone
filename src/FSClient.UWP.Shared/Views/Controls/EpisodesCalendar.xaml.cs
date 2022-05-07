namespace FSClient.UWP.Shared.Views.Controls
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;

    public sealed partial class EpisodesCalendar : UserControl
    {
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(IIncrementalCollection<EpisodeInfo, SeasonInfo>),
                typeof(EpisodesCalendar), new PropertyMetadata(null));

        public static readonly DependencyProperty ShowCalendarHelpInfoProperty =
            DependencyProperty.Register(nameof(ShowCalendarHelpInfo), typeof(bool), typeof(EpisodesCalendar),
                new PropertyMetadata(false));

        public EpisodesCalendar()
        {
            InitializeComponent();
        }

        public IIncrementalCollection<EpisodeInfo, SeasonInfo>? Source
        {
            get => (IIncrementalCollection<EpisodeInfo, SeasonInfo>?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public bool ShowCalendarHelpInfo
        {
            get => (bool)GetValue(ShowCalendarHelpInfoProperty);
            set => SetValue(ShowCalendarHelpInfoProperty, value);
        }
    }
}

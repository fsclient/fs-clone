namespace FSClient.UWP.Shared.Services
{
    using System;

#if WINUI3
    using Microsoft.UI.Xaml.Navigation;
#else
    using Windows.UI.Xaml.Navigation;
#endif

    public class HistoryNavigationItem : NavigationItem
    {
        public HistoryNavigationItem(string title, Type type) : base(title, type)
        {
        }

        public NavigationMode Mode { get; set; }

        public DateTimeOffset Time { get; set; }

        public override string ToShortString()
        {
            return $"{ModeAsGlyph(Mode).TrimEnd()}{Time.UtcDateTime:hh\\:mm\\:ss}-{base.ToShortString()}";
        }

        public override string ToString()
        {
            return $"{ModeAsGlyph(Mode)} {Time.UtcDateTime:hh\\:mm\\:ss} {base.ToString()}";
        }

        private string ModeAsGlyph(NavigationMode navigationMode)
        {
            return navigationMode switch
            {
                NavigationMode.Back => "<-",
                NavigationMode.Forward => "->",
                NavigationMode.New => "+ ",
                NavigationMode.Refresh => "o ",
                _ => "? ",
            };
        }
    }
}

namespace FSClient.UWP.Shared.Views.Themes
{
#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    public partial class Styles : ResourceDictionary
    {
        public Styles()
        {
            InitializeComponent();

            SetupBasedOnResources();
        }

        private void SetupBasedOnResources()
        {
#pragma warning disable IDE0003 // Remove qualification
            if (this.TryGetValue("ClientAppBarToggleButtonStyle", out var clientAppBarToggleButtonStyleObj)
                && this.TryGetValue("AppBarToggleButtonRevealStyle", out var appBarToggleButtonRevealStyle))
            {
                ((Style)clientAppBarToggleButtonStyleObj).BasedOn = (Style)appBarToggleButtonRevealStyle;
            }

            if (this.TryGetValue("ClientAppBarButtonStyle", out var clientAppBarButtonStyle)
                && this.TryGetValue("AppBarButtonRevealStyle", out var appBarButtonRevealStyle))
            {
                ((Style)clientAppBarButtonStyle).BasedOn = (Style)appBarButtonRevealStyle;
            }

            if (this.TryGetValue("ClientToggleButtonStyle", out var clientToggleButtonStyle)
                && this.TryGetValue("ToggleButtonRevealStyle", out var toggleButtonRevealStyle))
            {
                ((Style)clientToggleButtonStyle).BasedOn = (Style)toggleButtonRevealStyle;
            }

            if (this.TryGetValue("ClientButtonStyle", out var clientButtonStyle)
                && this.TryGetValue("ButtonRevealStyle", out var buttonRevealStyle))
            {
                ((Style)clientButtonStyle).BasedOn = (Style)buttonRevealStyle;
            }
#pragma warning restore IDE0003 // Remove qualification
        }
    }
}

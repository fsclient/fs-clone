namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Resources.Core;
    using Windows.Globalization;
#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Views.Pages;

    /// <inheritdoc/>
    public class UWPAppLanguageService : IAppLanguageService
    {
        private readonly IWindowsNavigationService navigationService;
        private string langCache;

        public UWPAppLanguageService(IWindowsNavigationService navigationService)
        {
            this.navigationService = navigationService;

            langCache = ApplicationLanguages.PrimaryLanguageOverride
                ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }

        public ValueTask ApplyLanguageAsync(string? name)
        {
            name ??= "ru-RU";

            CultureInfo.DefaultThreadCurrentCulture =
                CultureInfo.DefaultThreadCurrentUICulture =
                    CultureInfo.CurrentCulture =
                        CultureInfo.CurrentUICulture = new CultureInfo(name);
            langCache = ApplicationLanguages.PrimaryLanguageOverride = name;

            // If window is not initized yet, we don't need to Reset resource context
            if (Window.Current?.Content == null
                || !navigationService.HasAnyPage
                || navigationService.RootFrame.CurrentSourcePageType is not Type currentPage)
            {
                return new ValueTask();
            }

            var currentPageParameter =
                navigationService.PagesHistory.LastOrDefault(i => i.Type == currentPage)?.Parameter;

            return DispatcherHelper.GetForCurrentOrMainView()
                .CheckBeginInvokeOnUI(() =>
                {
                    ResourceContext.GetForCurrentView().Reset();
                    ResourceContext.GetForViewIndependentUse().Reset();

                    navigationService.ResetFrame();

                    var page = new MainPage();
                    Window.Current.Content = page;
                    page.Loaded += (s, a) => navigationService.Navigate(currentPage, currentPageParameter);
                });
        }

        public IEnumerable<string> GetAvailableLanguages()
        {
            return ApplicationLanguages.ManifestLanguages;
        }

        public string GetCurrentLanguage()
        {
            return langCache;
        }
    }
}

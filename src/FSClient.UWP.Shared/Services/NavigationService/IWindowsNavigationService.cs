namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;

#if WINUI3
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Services;
    public interface IWindowsNavigationService : INavigationService
    {
        IReadOnlyCollection<HistoryNavigationItem> PagesHistory { get; }
        Frame RootFrame { get; }

        bool Navigate<TPage>(object? parameter = null) where TPage : Page;
        bool Navigate(Type pageType, object? parameter = null);

        void ResetFrame();
    }
}

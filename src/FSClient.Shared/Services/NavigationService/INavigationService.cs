namespace FSClient.Shared.Services
{
    using System;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    public interface INavigationService
    {
        bool HasAnyPage { get; }
        event EventHandler<GoBackRequestedEventArgs>? GoBackRequested;

        Task<bool> GoBack(bool exitAllowed);

        bool Navigate(NavigationPageType pageType, object? parameter = null);

        bool GoForward();
    }
}

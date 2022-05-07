namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;

    using Windows.UI.ViewManagement;

#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    using FSClient.Shared.Services;

    public partial class ManagedWindow
    {
        private static readonly ConcurrentDictionary<int, ManagedWindow> windows =
            new ConcurrentDictionary<int, ManagedWindow>();

        private static readonly ConcurrentDictionary<Type, ManagedWindow> typedWindows =
            new ConcurrentDictionary<Type, ManagedWindow>();

        public static ManagedWindow? GetCurrent(Window window)
        {
            try
            {
                var view = ApplicationView.GetForCurrentView();
                if (view == null)
                {
                    return null;
                }

                return windows.GetOrAdd(view.Id, i =>
                {
                    var managedWindow = new ManagedWindow();
                    managedWindow.InitializeFromCurrent(window);
                    return managedWindow;
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
                return null;
            }
        }

        public static ManagedWindow GetOrCreate<TPage>()
            where TPage : UIElement, new()
        {
            return typedWindows.GetOrAdd(typeof(TPage), type => new TypedWindow<TPage>());
        }

        private class TypedWindow<TPage>
            : ManagedWindow where TPage : UIElement, new()
        {
            [MaybeNull]
            [AllowNull]
            public TPage Content { get; private set; }

            public TypedWindow()
            {
                Content = default;
                Initialized += Window_Initialized;
                Destroyed += Window_Destroyed;
            }

            private void Window_Destroyed(object? sender, EventArgs e)
            {
                try
                {
                    Content = null;
                    SetContent(null);
                }
                catch (Exception ex) when (Logger.Initialized)
                {
                    Logger.Instance.LogWarning(ex);
                }
            }

            private void Window_Initialized(object? sender, EventArgs e)
            {
                Content = new TPage();
                SetContent(Content);
            }
        }
    }
}

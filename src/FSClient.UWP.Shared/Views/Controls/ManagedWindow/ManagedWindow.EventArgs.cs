namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

    public class WindowShowedEventArgs : EventArgs
    {
        public object? Parameter { get; }
        public bool InCompactOverlay { get; }

        public WindowShowedEventArgs(object? parameter, bool inCompactOverlay)
        {
            Parameter = parameter;
            InCompactOverlay = inCompactOverlay;
        }
    }

    public class FocusChangedEventArgs : EventArgs
    {
        public bool IsVisible { get; }
        public bool IsFocused { get; }

        public FocusChangedEventArgs(bool isVisible, bool isFocused)
        {
            IsVisible = isVisible;
            IsFocused = isFocused;
        }
    }

    public class WindowModeChangedEventArgs : EventArgs
    {
        public WindowMode WindowMode { get; }

        public WindowModeChangedEventArgs(WindowMode value)
        {
            WindowMode = value;
        }
    }
}

namespace FSClient.UWP.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Foundation;
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Input;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Input;
#endif

    public static class VisualTreeExtensions
    {
        private static readonly bool HasIsLoaded =
            Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent(
                typeof(FrameworkElement).FullName,
                nameof(FrameworkElement.IsLoaded));

        private static readonly bool HasTryFocusAsync =
            Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent(
                typeof(FocusManager).FullName,
                nameof(FocusManager.TryFocusAsync));

        public static bool IsChildOf<TFrameworkElement>(this TFrameworkElement element, DependencyObject root,
            bool isDirect = false)
            where TFrameworkElement : FrameworkElement
        {
            return isDirect
                ? FindDirectVisualChildren<TFrameworkElement>(root).Any(el => el == element)
                : FindVisualChildren<TFrameworkElement>(root).Any(el => el == element);
        }

        public static IEnumerable<T> FindDirectVisualChildren<T>(this DependencyObject depObj)
            where T : DependencyObject
        {
            if (depObj == null)
            {
                yield break;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }
            }
        }

        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject depObj)
            where T : DependencyObject
        {
            if (depObj == null)
            {
                yield break;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        [return: MaybeNull]
        public static T FindVisualChild<T>(this DependencyObject depObj)
            where T : DependencyObject
        {
            return depObj.FindVisualChildren<T>().FirstOrDefault();
        }

        [return: MaybeNull]
        public static T FindVisualChild<T>(this DependencyObject depObj, string name)
            where T : FrameworkElement
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return depObj.FindVisualChildren<T>().FirstOrDefault(o =>
                name.Equals(o.Name, StringComparison.OrdinalIgnoreCase));
        }

        public static FrameworkElement? FindVisualChild(this DependencyObject depObj, string name)
        {
            return depObj.FindVisualChild<FrameworkElement>(name);
        }

        [return: MaybeNull]
        public static T FindAscendant<T>(this FrameworkElement element)
            where T : FrameworkElement
        {
            return (element?.Parent) switch
            {
                T node => node,
                FrameworkElement parent => parent.FindAscendant<T>(),
                _ => null,
            };
        }

        [return: MaybeNull]
        public static T FindVisualAscendant<T>(this FrameworkElement element, string? name = null)
            where T : FrameworkElement
        {
            return (VisualTreeHelper.GetParent(element)) switch
            {
                T node when name == null || node.Name == name => node,
                FrameworkElement parent => parent.FindVisualAscendant<T>(name),
                _ => null,
            };
        }

        public static IEnumerable<T> FindVisualAscendants<T>(this FrameworkElement element)
            where T : FrameworkElement
        {
            while (true)
            {
                switch (VisualTreeHelper.GetParent(element))
                {
                    case FrameworkElement parent:
                        element = parent;
                        if (element is T founded)
                        {
                            yield return founded;
                        }

                        continue;
                    default:
                        yield break;
                }
            }
        }

        public static bool IsChildOf(this DependencyObject? depObj, DependencyObject container)
        {
            if (depObj == null)
            {
                return false;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(container); i++)
            {
                var child = VisualTreeHelper.GetChild(container, i);
                if (child == depObj)
                {
                    return true;
                }

                foreach (var childOfChild in FindVisualChildren<FrameworkElement>(child))
                {
                    if (depObj.IsChildOf(childOfChild))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsChildVisibileToUser(
            this FrameworkElement? container,
            FrameworkElement? element)
        {
            if (element == null || container == null)
            {
                return false;
            }

            var elementBounds = element.TransformToVisual(container)
                .TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
            var containerBounds = new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight);

            return elementBounds.Top < containerBounds.Bottom && elementBounds.Bottom > containerBounds.Top;
        }

        public static ValueTask WaitForLoadedAsync(
            this FrameworkElement element,
            CancellationToken cancellationToken = default)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (HasIsLoaded)
            {
                return WaitForLoadedInternalAsync(element, cancellationToken);
            }
            else
            {
                return WaitForLoadedInternalSlowAsync(element, cancellationToken);
            }

            static ValueTask WaitForLoadedInternalAsync(FrameworkElement element, CancellationToken cancellationToken)
            {
                if (element.IsLoaded())
                {
                    return new ValueTask();
                }

                var taskCS = new TaskCompletionSource<bool>();
                cancellationToken.Register(async () =>
                {
                    taskCS.TrySetCanceled(cancellationToken);
                    await element.Dispatcher.CheckBeginInvokeOnUI(() => element.Loaded -= Element_Loaded);
                });

                element.Loaded += Element_Loaded;

                return new ValueTask(taskCS.Task);

                void Element_Loaded(object sender, object _)
                {
                    taskCS.TrySetResult(true);
                    ((FrameworkElement)sender).Loaded -= Element_Loaded;
                }
            }

            static async ValueTask WaitForLoadedInternalSlowAsync(FrameworkElement element, CancellationToken cancellationToken)
            {
                await Task.Yield();
                await WaitForLoadedInternalAsync(element, cancellationToken);
            }
        }

        public static bool IsLoaded(this FrameworkElement element)
        {
            return HasIsLoaded ? element.IsLoaded : VisualTreeHelper.GetParent(element) != null;
        }

        public static async Task<bool> TryFocusAsync(this Control control, FocusState focusState)
        {
            await control.WaitForLoadedAsync();
            if (HasTryFocusAsync)
            {
                var result = await FocusManager.TryFocusAsync(control, focusState);
                return result.Succeeded;
            }

            return control.Focus(focusState);
        }
    }
}

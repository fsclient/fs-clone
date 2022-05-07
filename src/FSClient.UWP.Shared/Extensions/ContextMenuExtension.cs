namespace FSClient.UWP.Shared.Extensions
{
    using System;

    using Windows.Foundation.Metadata;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.UWP.Shared.Helpers;

    public static class ContextMenuExtension
    {
        private static readonly bool ContextFlyoutAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(UIElement).FullName,
                nameof(UIElement.ContextFlyout));

        public static readonly DependencyProperty ContextFlyoutProperty =
            DependencyProperty.RegisterAttached("ContextFlyout", typeof(FlyoutBase), typeof(ContextMenuExtension),
                new PropertyMetadata(null, OnContextFlyoutChanged));

        public static FlyoutBase GetContextFlyout(UIElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return (FlyoutBase)element.GetValue(ContextFlyoutProperty);
        }

        public static void SetContextFlyout(UIElement element, FlyoutBase value)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(ContextFlyoutProperty, value);
        }

        private static void OnContextFlyoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = (FrameworkElement)d;
            if (e == null
                || element == null
                || e.NewValue == null
                || !ContextFlyoutAvailable)
            {
                return;
            }

            var flyout = (FlyoutBase?)e.NewValue;

            if (element.IsLoaded())
            {
                Setup(element, flyout);
            }
            else
            {
                element.Loaded += Element_Loaded;
            }
        }

        private static void Setup(FrameworkElement element, FlyoutBase? flyout)
        {
            if (element.FindVisualAscendant<SelectorItem>() is SelectorItem selectorItem)
            {
                selectorItem.ContextFlyout = flyout;
            }
            else
            {
                element.ContextFlyout = flyout;
            }
        }

        private static void Element_Loaded(object sender, RoutedEventArgs e)
        {
            var element = (FrameworkElement)sender;
            element.Loaded -= Element_Loaded;

            Setup(element, GetContextFlyout(element));
        }
    }
}

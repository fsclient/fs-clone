namespace FSClient.UWP.Shared.Views.Controls
{
    using Windows.Foundation.Metadata;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Markup;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Markup;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.UWP.Shared.Helpers;

    [ContentProperty(Name = nameof(Content))]
    public class MenuFlyoutContentItem : MenuFlyoutItem
    {
        private readonly bool isGettingFocusAvailable
            = ApiInformation.IsEventPresent(typeof(UIElement).FullName, nameof(GettingFocus));

        public MenuFlyoutContentItem()
        {
            DefaultStyleKey = nameof(MenuFlyoutContentItem);

            if (isGettingFocusAvailable)
            {
                GettingFocus += MenuFlyoutContentItem_GettingFocus;
            }
        }

        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(nameof(Content), typeof(object), typeof(MenuFlyoutContentItem),
                new PropertyMetadata(null));

        private void MenuFlyoutContentItem_GettingFocus(UIElement sender, GettingFocusEventArgs args)
        {
            if (Content is DependencyObject dependencyObject
                && !args.OldFocusedElement.IsChildOf(this))
            {
                args.Handled = args.TrySetNewFocusedElement(dependencyObject);
            }
        }
    }
}

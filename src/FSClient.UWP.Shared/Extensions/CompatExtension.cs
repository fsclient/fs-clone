// https://blogs.msdn.microsoft.com/wsdevsol/2016/09/14/combobox-from-an-appbarbutton-loses-mouse-input-on-1607/

namespace FSClient.UWP.Shared.Extensions
{
    using Windows.Foundation.Metadata;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Input;
#endif

    using FSClient.UWP.Shared.Helpers;
    public static class CompatExtension
    {
        #region KeyboardAccelerators

        public static readonly bool KeyboardAcceleratorsAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(UIElement).FullName,
                nameof(UIElement.KeyboardAccelerators));

        #endregion

        #region Icon

        public static readonly bool MenuFlyoutItemIconAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(MenuFlyoutItem).FullName,
                nameof(MenuFlyoutItem.Icon));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.RegisterAttached(
                nameof(MenuFlyoutItem.Icon),
                typeof(IconElement),
                typeof(CompatExtension),
                new PropertyMetadata(null, IconChanged));

        public static IconElement? GetIcon(DependencyObject obj)
        {
            return obj.GetValue(IconProperty) as IconElement;
        }

        public static void SetIcon(DependencyObject obj, IconElement value)
        {
            obj.SetValue(IconProperty, value);
        }

        private static void IconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            switch (d)
            {
                case MenuFlyoutItem menuItem
                    when MenuFlyoutItemIconAvailable
                         && e.NewValue is IconElement newIconElement:
                    menuItem.Icon = newIconElement;
                    break;
            }
        }

        #endregion

        #region AccessKey

        public static readonly bool AccessKeyAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(UIElement).FullName,
                nameof(UIElement.AccessKey));

        public static readonly DependencyProperty AccessKeyProperty =
            DependencyProperty.RegisterAttached(
                nameof(UIElement.AccessKey),
                typeof(string),
                typeof(CompatExtension),
                new PropertyMetadata(null, AccessKeyChanged));

        public static string? GetAccessKey(DependencyObject obj)
        {
            return obj.GetValue(AccessKeyProperty) as string;
        }

        public static void SetAccessKey(DependencyObject obj, string value)
        {
            obj.SetValue(AccessKeyProperty, value);
        }

        private static void AccessKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (AccessKeyAvailable
                && d is UIElement element)
            {
                element.AccessKey = e.NewValue as string;
            }
        }

        #endregion

        #region AccessKeyScopeOwner

        public static readonly bool AccessKeyScopeOwnerAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(UIElement).FullName,
                nameof(UIElement.AccessKeyScopeOwner));

        public static readonly DependencyProperty AccessKeyScopeOwnerProperty =
            DependencyProperty.RegisterAttached(
                nameof(UIElement.AccessKeyScopeOwner),
                typeof(UIElement),
                typeof(CompatExtension),
                new PropertyMetadata(null, AccessKeyScopeOwnerChanged));

        public static UIElement? GetAccessKeyScopeOwner(DependencyObject obj)
        {
            return obj.GetValue(AccessKeyScopeOwnerProperty) as UIElement;
        }

        public static void SetAccessKeyScopeOwner(DependencyObject obj, UIElement value)
        {
            obj.SetValue(AccessKeyScopeOwnerProperty, value);
        }

        private static void AccessKeyScopeOwnerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!AccessKeyScopeOwnerAvailable
                || d is not UIElement element)
            {
                return;
            }

            if (e.NewValue is not FrameworkElement newValue)
            {
                if (e.OldValue is FrameworkElement oldValue)
                {
                    oldValue.IsAccessKeyScope = false;
                }

                element.AccessKeyScopeOwner = null;
                return;
            }

            if (newValue is Control)
            {
                SetupScoreOwner(element, newValue);
            }
            else if (newValue.IsLoaded())
            {
                newValue = newValue.FindVisualAscendant<Control>() ?? newValue;
                SetupScoreOwner(element, newValue);
            }
            else
            {
                newValue.Loaded += Element_Loaded;

                void Element_Loaded(object _, object __)
                {
                    newValue.Loaded -= Element_Loaded;

                    newValue = newValue.FindVisualAscendant<Control>() ?? newValue;
                    SetupScoreOwner(element, newValue);
                }
            }

            static void SetupScoreOwner(UIElement inElement, FrameworkElement inNewValue)
            {
                if (inNewValue != null)
                {
                    inNewValue.IsAccessKeyScope = true;
                    inElement.AccessKeyScopeOwner = inNewValue;
                }
            }
        }

        #endregion

        #region IsFocusEngagementEnabled

        public static readonly bool IsFocusEngagementEnabledAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(Control).FullName,
                nameof(Control.IsFocusEngagementEnabled));

        public static readonly DependencyProperty IsFocusEngagementEnabledProperty =
            DependencyProperty.RegisterAttached(
                nameof(Control.IsFocusEngagementEnabled),
                typeof(bool),
                typeof(CompatExtension),
                new PropertyMetadata(0, IsFocusEngagementEnabledChanged));

        public static bool GetIsFocusEngagementEnabled(DependencyObject obj)
        {
            return obj.GetValue(IsFocusEngagementEnabledProperty) as bool? ?? false;
        }

        public static void SetIsFocusEngagementEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsFocusEngagementEnabledProperty, value);
        }

        private static void IsFocusEngagementEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (IsFocusEngagementEnabledAvailable
                && d is Control element
                && e.NewValue is bool newValue)
            {
                element.IsFocusEngagementEnabled = newValue;
            }
        }

        #endregion

        #region FocusVisualMargin

        public static readonly bool FocusVisualMarginAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(FrameworkElement).FullName,
                nameof(FrameworkElement.FocusVisualMargin));

        public static readonly DependencyProperty FocusVisualMarginProperty =
            DependencyProperty.RegisterAttached(
                nameof(FrameworkElement.FocusVisualMargin),
                typeof(Thickness),
                typeof(CompatExtension),
                new PropertyMetadata(0, FocusVisualMarginChanged));

        public static Thickness GetFocusVisualMargin(DependencyObject obj)
        {
            return obj.GetValue(FocusVisualMarginProperty) as Thickness? ?? new Thickness();
        }

        public static void SetFocusVisualMargin(DependencyObject obj, Thickness value)
        {
            obj.SetValue(FocusVisualMarginProperty, value);
        }

        private static void FocusVisualMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (FocusVisualMarginAvailable
                && d is FrameworkElement element
                && e.NewValue is Thickness newValue)
            {
                element.FocusVisualMargin = newValue;
            }
        }

        #endregion

        #region XYFocusKeyboardNavigation

        public static readonly bool XYFocusKeyboardNavigationAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(UIElement).FullName,
                nameof(Control.XYFocusKeyboardNavigation));

        public static readonly DependencyProperty XYFocusKeyboardNavigationProperty =
            DependencyProperty.RegisterAttached(
                nameof(Control.XYFocusKeyboardNavigation),
                typeof(bool),
                typeof(CompatExtension),
                new PropertyMetadata(false, XYFocusKeyboardNavigationChanged));

        public static bool GetXYFocusKeyboardNavigation(DependencyObject obj)
        {
            return obj.GetValue(XYFocusKeyboardNavigationProperty) as bool? ?? false;
        }

        public static void SetXYFocusKeyboardNavigation(DependencyObject obj, bool value)
        {
            obj.SetValue(XYFocusKeyboardNavigationProperty, value);
        }

        private static void XYFocusKeyboardNavigationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (XYFocusKeyboardNavigationAvailable
                && d is UIElement element
                && e.NewValue is bool newValue)
            {
                element.XYFocusKeyboardNavigation = newValue
                    ? XYFocusKeyboardNavigationMode.Enabled
                    : XYFocusKeyboardNavigationMode.Auto;
            }
        }

        #endregion

        #region XYFocus

        public static readonly bool XYFocusLeftAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(Control).FullName,
                nameof(Control.XYFocusLeft));

        public static readonly bool XYFocusRightAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(Control).FullName,
                nameof(Control.XYFocusRight));

        public static readonly bool XYFocusDownAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(Control).FullName,
                nameof(Control.XYFocusDown));

        public static readonly bool XYFocusUpAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(Control).FullName,
                nameof(Control.XYFocusUp));

        public static readonly DependencyProperty XYFocusLeftProperty =
            DependencyProperty.RegisterAttached(
                nameof(Control.XYFocusLeft),
                typeof(DependencyObject),
                typeof(CompatExtension),
                new PropertyMetadata(null, XYFocusLeftChanged));

        public static DependencyObject? GetXYFocusLeft(DependencyObject obj)
        {
            return obj.GetValue(XYFocusLeftProperty) as DependencyObject;
        }

        public static void SetXYFocusLeft(DependencyObject obj, DependencyObject value)
        {
            obj.SetValue(XYFocusLeftProperty, value);
        }

        private static void XYFocusLeftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (XYFocusLeftAvailable
                && d is Control element)
            {
                element.XYFocusLeft = e.NewValue as DependencyObject;
            }
        }

        public static readonly DependencyProperty XYFocusRightProperty =
            DependencyProperty.RegisterAttached(
                nameof(Control.XYFocusRight),
                typeof(DependencyObject),
                typeof(CompatExtension),
                new PropertyMetadata(null, XYFocusRightChanged));

        public static DependencyObject? GetXYFocusRight(DependencyObject obj)
        {
            return obj.GetValue(XYFocusRightProperty) as DependencyObject;
        }

        public static void SetXYFocusRight(DependencyObject obj, DependencyObject value)
        {
            obj.SetValue(XYFocusRightProperty, value);
        }

        private static void XYFocusRightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (XYFocusRightAvailable
                && d is Control element)
            {
                element.XYFocusRight = e.NewValue as DependencyObject;
            }
        }

        public static readonly DependencyProperty XYFocusDownProperty =
            DependencyProperty.RegisterAttached(
                nameof(Control.XYFocusDown),
                typeof(DependencyObject),
                typeof(CompatExtension),
                new PropertyMetadata(null, XYFocusDownChanged));

        public static DependencyObject? GetXYFocusDown(DependencyObject obj)
        {
            return obj.GetValue(XYFocusDownProperty) as DependencyObject;
        }

        public static void SetXYFocusDown(DependencyObject obj, DependencyObject value)
        {
            obj.SetValue(XYFocusDownProperty, value);
        }

        private static void XYFocusDownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (XYFocusDownAvailable
                && d is Control element)
            {
                element.XYFocusDown = e.NewValue as DependencyObject;
            }
        }

        public static readonly DependencyProperty XYFocusUpProperty =
            DependencyProperty.RegisterAttached(
                nameof(Control.XYFocusUp),
                typeof(DependencyObject),
                typeof(CompatExtension),
                new PropertyMetadata(null, XYFocusUpChanged));

        public static DependencyObject? GetXYFocusUp(DependencyObject obj)
        {
            return obj.GetValue(XYFocusUpProperty) as DependencyObject;
        }

        public static void SetXYFocusUp(DependencyObject obj, DependencyObject value)
        {
            obj.SetValue(XYFocusUpProperty, value);
        }

        private static void XYFocusUpChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (XYFocusUpAvailable
                && d is Control element)
            {
                element.XYFocusUp = e.NewValue as DependencyObject;
            }
        }

        #endregion

        #region AllowFocusOnInteraction

        public static readonly bool AllowFocusOnInteractionAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(FrameworkElement).FullName,
                nameof(FrameworkElement.AllowFocusOnInteraction));

        public static readonly DependencyProperty AllowFocusOnInteractionProperty =
            DependencyProperty.RegisterAttached(
                nameof(FrameworkElement.AllowFocusOnInteraction),
                typeof(bool),
                typeof(CompatExtension),
                new PropertyMetadata(0, AllowFocusOnInteractionChanged));

        public static bool GetAllowFocusOnInteraction(DependencyObject obj)
        {
            return obj.GetValue(AllowFocusOnInteractionProperty) as bool? ?? false;
        }

        public static void SetAllowFocusOnInteraction(DependencyObject obj, bool value)
        {
            obj.SetValue(AllowFocusOnInteractionProperty, value);
        }

        private static void AllowFocusOnInteractionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (AllowFocusOnInteractionAvailable
                && d is FrameworkElement element
                && e.NewValue is bool newValue)
            {
                element.AllowFocusOnInteraction = newValue;
            }
        }

        #endregion

        #region LabelPosition

        public static readonly bool IsLabelPositionAvailable =
            ApiInformation.IsPropertyPresent(
                typeof(AppBarToggleButton).FullName,
                nameof(AppBarToggleButton.LabelPosition));

        public static readonly DependencyProperty LabelPositionCollapsedProperty =
            DependencyProperty.Register("LabelPositionCollapsed", typeof(bool), typeof(CompatExtension),
                new PropertyMetadata(false, LabelPositionCollapsedPropertyChanged));

        public static bool GetLabelPositionCollapsed(DependencyObject obj)
        {
            return obj.GetValue(LabelPositionCollapsedProperty) as bool? ?? false;
        }

        public static void SetLabelPositionCollapsed(DependencyObject obj, bool value)
        {
            obj.SetValue(LabelPositionCollapsedProperty, value);
        }

        private static void LabelPositionCollapsedPropertyChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (!IsLabelPositionAvailable)
            {
                return;
            }

            if (d is AppBarToggleButton appBarToggleButton)
            {
                appBarToggleButton.LabelPosition =
                    (bool)e.NewValue ? CommandBarLabelPosition.Collapsed : CommandBarLabelPosition.Default;
            }
            else if (d is AppBarButton appBarButton)
            {
                appBarButton.LabelPosition =
                    (bool)e.NewValue ? CommandBarLabelPosition.Collapsed : CommandBarLabelPosition.Default;
            }
        }

        #endregion
    }
}

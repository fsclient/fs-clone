namespace FSClient.UWP.Shared.Extensions
{
    using System.Threading.Tasks;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    public static class ContentExtension
    {
        public static readonly DependencyProperty AutoUpdateDataTemplateProperty
            = DependencyProperty.Register("AutoUpdateDataTemplate", typeof(bool), typeof(ContentExtension),
                PropertyMetadata.Create(false, AutoUpdateDataTemplateChanged));

        public static bool GetAutoUpdateDataTemplate(DependencyObject sender)
        {
            return (bool)sender.GetValue(AutoUpdateDataTemplateProperty);
        }

        public static void SetAutoUpdateDataTemplate(DependencyObject sender, bool value)
        {
            sender.SetValue(AutoUpdateDataTemplateProperty, value);
        }

        private static void AutoUpdateDataTemplateChanged(DependencyObject sender,
            DependencyPropertyChangedEventArgs args)
        {
            switch (sender)
            {
                case ContentPresenter cp:
                    if ((bool)args.NewValue)
                    {
                        cp.Tag = cp.RegisterPropertyChangedCallback(ContentPresenter.ContentProperty, ContentChanged);
                    }
                    else if (cp.Tag is long token)
                    {
                        cp.UnregisterPropertyChangedCallback(ContentPresenter.ContentProperty, token);
                    }

                    break;
                case ContentControl cc:
                    if ((bool)args.NewValue)
                    {
                        cc.Tag = cc.RegisterPropertyChangedCallback(ContentPresenter.ContentProperty, ContentChanged);
                    }
                    else if (cc.Tag is long token)
                    {
                        cc.UnregisterPropertyChangedCallback(ContentPresenter.ContentProperty, token);
                    }

                    break;
            }
        }

        private static async void ContentChanged(DependencyObject sender, DependencyProperty prop)
        {
            switch (sender)
            {
                case ContentPresenter cp:
                    cp.ContentTemplate = null;
                    await Task.Yield();
                    cp.ContentTemplate = cp.ContentTemplateSelector.SelectTemplate(sender.GetValue(prop), sender);
                    break;
                case ContentControl cc:
                    cc.ContentTemplate = null;
                    await Task.Yield();
                    cc.ContentTemplate = cc.ContentTemplateSelector.SelectTemplate(sender.GetValue(prop), sender);
                    break;
            }
        }
    }
}

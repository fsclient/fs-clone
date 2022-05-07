namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Linq;
    using System.Windows.Input;
    using System.Collections.Generic;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public sealed partial class AuthUserControl : UserControl
    {
        public AuthUserControl()
        {
            InitializeComponent();
            EnsureStates(false);
        }

        public User? User
        {
            get => (User?)GetValue(UserProperty);
            set => SetValue(UserProperty, value);
        }

        public static readonly DependencyProperty UserProperty =
            DependencyProperty.Register(nameof(User), typeof(User), typeof(AuthUserControl),
                new PropertyMetadata(null, OnEnsureStates));

        public ICommand? LogoutCommand
        {
            get => (ICommand?)GetValue(LogoutCommandProperty);
            set => SetValue(LogoutCommandProperty, value);
        }

        public static readonly DependencyProperty LogoutCommandProperty =
            DependencyProperty.Register(nameof(LogoutCommand), typeof(ICommand), typeof(AuthUserControl),
                new PropertyMetadata(null));

        public ICommand? RegisterCommand
        {
            get => (ICommand?)GetValue(RegisterCommandProperty);
            set => SetValue(RegisterCommandProperty, value);
        }

        public static readonly DependencyProperty RegisterCommandProperty =
            DependencyProperty.Register(nameof(RegisterCommand), typeof(ICommand), typeof(AuthUserControl),
                new PropertyMetadata(null));

        public ICommand? LoginCommand
        {
            get => (ICommand?)GetValue(LoginCommandProperty);
            set => SetValue(LoginCommandProperty, value);
        }

        public static readonly DependencyProperty LoginCommandProperty =
            DependencyProperty.Register(nameof(LoginCommand), typeof(ICommand), typeof(AuthUserControl),
                new PropertyMetadata(null));

        public Site Site
        {
            get => (Site)GetValue(SiteProperty);
            set => SetValue(SiteProperty, value);
        }

        public static readonly DependencyProperty SiteProperty =
            DependencyProperty.Register(nameof(Site), typeof(Site), typeof(AuthUserControl),
                new PropertyMetadata(Site.Any, OnEnsureStates));

        public IEnumerable<AuthModel> AuthModels
        {
            get => (IEnumerable<AuthModel>)GetValue(AuthModelsProperty);
            set => SetValue(AuthModelsProperty, value);
        }

        public static readonly DependencyProperty AuthModelsProperty =
            DependencyProperty.Register(nameof(AuthModels), typeof(int), typeof(AuthUserControl),
                new PropertyMetadata(Enumerable.Empty<AuthModel>(), OnEnsureStates));

        public string? ProviderRequirements
        {
            get => (string?)GetValue(ProviderRequirementsProperty);
            set => SetValue(ProviderRequirementsProperty, value);
        }

        public static readonly DependencyProperty ProviderRequirementsProperty =
            DependencyProperty.Register(nameof(ProviderRequirements), typeof(string), typeof(AuthUserControl),
                new PropertyMetadata(null, OnEnsureStates));

        private static void OnEnsureStates(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AuthUserControl)d).EnsureStates(true);
        }

        private void EnsureStates(bool useTransitions)
        {
            var modelsCount = AuthModels.Count();
            var user = User;
            VisualStateManager.GoToState(this, user?.HasProStatus == true ? "UserWithProPanelVisibleState"
                : user != null ? "UserPanelVisibleState"
                : modelsCount == 1 ? "OneAuthModelVisibleState"
                : modelsCount > 1 ? "MoreAuthModelVisibleState"
                : "NoPanelVisibleState", useTransitions);

            VisualStateManager.GoToState(this, string.IsNullOrEmpty(ProviderRequirements)
                ? "ProviderRequirementsFieldHidden" : "ProviderRequirementsFieldVisible", useTransitions);
        }
    }
}

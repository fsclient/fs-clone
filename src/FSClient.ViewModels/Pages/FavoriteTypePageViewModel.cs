namespace FSClient.ViewModels.Pages
{
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;

    public class FavoritesTypePageViewModel : ViewModelBase
    {
        public FavoritesTypePageViewModel(FavoriteListKind kind)
        {
            FavoriteListKind = kind;
        }

        public FavoriteListKind FavoriteListKind { get; }

        public string Caption => FavoriteListKind.GetDisplayName()!;

        public bool HasAnyItem => Content?.Any() ?? false;

        public IEnumerable<object> Content
        {
            get => Get<IEnumerable<object>>();
            set
            {
                if (Set(value))
                {
                    OnPropertyChanged(nameof(HasAnyItem));
                }
            }
        }

        public override string ToString()
        {
            return Caption;
        }
    }
}

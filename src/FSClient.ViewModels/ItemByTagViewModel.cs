namespace FSClient.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;
    using FSClient.ViewModels.Pages;

    public class ItemByTagViewModel : ViewModelBase, IStateSaveable
    {
        private readonly IItemManager itemManager;
        private readonly IIncrementalCollectionFactory collectionFactory;

        public ItemByTagViewModel(
            IItemManager itemManager,
            IIncrementalCollectionFactory collectionFactory)
        {
            this.itemManager = itemManager;
            this.collectionFactory = collectionFactory;

            PreloadItemCommand = new AsyncCommand<ItemInfo>(
                (i, ct) => itemManager.PreloadItemAsync(i, PreloadItemStrategy.Poster, ct).AsTask());

            UpdateSourceCommand = new AsyncCommand(
                UpdateSourceAsync,
                AsyncCommandConflictBehaviour.Skip);
        }

        public TitledTag CurrentTag
        {
            get => Get<TitledTag>();
            set
            {
                if (Set(value))
                {
                    SectionPages = Array.Empty<SectionPageViewModel>();

                    if (value.Title is string title
                        && title.Length > 0)
                    {
                        Title = char.ToUpper(title[0]) + title[1..];
                    }
                    else
                    {
                        Title = value.Value;
                    }
                }
            }
        }

        public string? Title
        {
            get => Get<string>();
            private set => Set(value);
        }

        public SectionPageViewModel? CurrentPage
        {
            get => Get<SectionPageViewModel>();
            set => Set(value);
        }

        public IReadOnlyCollection<SectionPageViewModel> SectionPages
        {
            get => Get<IReadOnlyCollection<SectionPageViewModel>>(new List<SectionPageViewModel>());
            private set
            {
                if (Set(value))
                {
                    CurrentPage = value?.FirstOrDefault();
                }
            }
        }

        public AsyncCommand<ItemInfo> PreloadItemCommand { get; }

        public AsyncCommand UpdateSourceCommand { get; }

        public Uri? SaveStateToUri()
        {
            return UriParserHelper.GenerateUriFromTitledTag(CurrentTag);
        }

        private async Task UpdateSourceAsync(CancellationToken cancellationToken)
        {
            var currentTag = CurrentTag;
            if (currentTag == TitledTag.Any)
            {
                SectionPages = Array.Empty<SectionPageViewModel>();
            }
            else
            {
                SectionPages = await itemManager
                    .GetSectionPageFiltersForTagAsync(CurrentTag.Site, currentTag, cancellationToken)
                    .ToFlatAsyncEnumerable()
                    .Select(filter => new SectionPageViewModel(itemManager, collectionFactory, filter))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}

namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Collections.Generic;
    using System.Linq;

#if UNO
    using Windows.UI.Text;
#elif WINUI3
    using Microsoft.UI.Text;
#endif
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Documents;
#else
    using Windows.UI.Text;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Documents;
#endif

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Views.Pages;
    using FSClient.ViewModels.Items;

    public partial class ItemPageDetails : Control
    {
        public ItemPageDetails()
        {
            DefaultStyleKey = nameof(ItemPageDetails);
        }

        protected override void OnApplyTemplate()
        {
            if (GetTemplateChild("PosterImage") is PosterImage posterImage)
            {
                posterImage.ImageOpened += ImageLoaded;
            }

            if (GetTemplateChild("PosterPanel") is StackPanel posterPanel)
            {
                posterPanel.SizeChanged += ImageLoaded;
            }

            if (GetTemplateChild("SimilarGrid") is GridView similarGrid)
            {
                similarGrid.ItemClick += NavigateToItem;
            }

            if (GetTemplateChild("FranchiseGrid") is GridView franchiseGrid)
            {
                franchiseGrid.ItemClick += NavigateToItem;
            }

            if (GetTemplateChild("ImagesFlipView") is FlipView imagesFlipView)
            {
                imagesFlipView.SizeChanged += ImagesFlipView_SizeChanged;
            }

            base.OnApplyTemplate();
        }

        private void ImagesFlipView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e == null
                || e.PreviousSize == e.NewSize)
            {
                return;
            }

            var f = (FlipView)sender;
            f.Height = f.ActualWidth / 16 * 9;
        }

        private void NavigateToItem(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ItemsListItemViewModel itemsListItemViewModel)
            {
                ViewModelLocator.Current.NavigationService.Navigate<ItemPage>(itemsListItemViewModel.ItemInfo);
            }
        }

        private void ImageLoaded(object s, object a)
        {
            if (GetTemplateChild("PosterPanel") is StackPanel posterPanel
                && GetTemplateChild("InfoPanel") is Grid infoPanel)
            {
                infoPanel.MaxHeight = posterPanel.ActualHeight + 8;
            }
        }

        private static void AddLine(Paragraph paragraph, string dt, string dd, bool last = false)
        {
            paragraph.Inlines.Add(
                new Run {Text = dt, FontWeight = FontWeights.Bold});
            paragraph.Inlines.Add(
                new Run {Text = dd});
            if (!last)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        private static void AddLine(Paragraph paragraph, string dt, IEnumerable<TitledTag> dd, TagType tagType,
            bool last = false)
        {
            paragraph.Inlines.Add(
                new Run {Text = dt, FontWeight = FontWeights.Bold});
            var list = dd.ToList();
            foreach (var titledLink in list)
            {
                var title = tagType == TagType.Genre ? titledLink.Title?.ToLower() : titledLink.Title;
                var run = new Run {Text = title + (titledLink.Title != list.Last().Title ? ", " : string.Empty)};
                if (titledLink.Value != null)
                {
                    var link = new Hyperlink {UnderlineStyle = UnderlineStyle.None, Foreground = paragraph.Foreground};

                    link.Inlines.Add(run);
                    link.Click += (s, a) =>
                        ViewModelLocator.Current.NavigationService.Navigate<ItemsByTagPage>(titledLink);
                    ToolTipService.SetToolTip(link, Strings.ItemPageDetails_SearchByTag);

                    paragraph.Inlines.Add(link);
                }
                else
                {
                    paragraph.Inlines.Add(run);
                }
            }

            if (!last)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        private void BuildInfoView(ItemInfo? item)
        {
            if (GetTemplateChild("InfoRichTextBlock") is not RichTextBlock infoRichTextBlock)
            {
                return;
            }

            infoRichTextBlock.Blocks.Clear();

            if (item == null)
            {
                return;
            }

            var par = new Paragraph();

            var link = new Hyperlink
            {
                UnderlineStyle = UnderlineStyle.None, Inlines = {new Run() {Text = item.Site.Title}}
            };
            link.Click += (s, a) => OpenItemInBrowserCommand?.Execute(item);
            ToolTipService.SetToolTip(link, Strings.ItemPageDetails_ProviderTagTooltip);
            par.Inlines.Add(new Run() {Text = $"{Strings.TagType_Provider}: ", FontWeight = FontWeights.Bold});
            par.Inlines.Add(link);
            par.Inlines.Add(new LineBreak());

            if (item.Section != Section.Any)
            {
                AddLine(par, $"{Strings.TagType_Section}: ", item.Section.Title);
            }

            if (item.Details.Year is int year)
            {
                var y = year.ToString();
                if (item.Details.YearEnd.HasValue)
                {
                    y += "-" + item.Details.YearEnd;
                }

                AddLine(par, $"{Strings.TagType_Year}: ", y);
            }

            var status = item.Details.Status;
            {
                var statusTags = new List<TitledTag>();
                if (status.Type != StatusType.Unknown)
                {
                    statusTags.Add(new TitledTag(status.Type.GetDisplayName()?.ToLower()));
                }

                var statusStr = status.ToString();
                if (!string.IsNullOrWhiteSpace(statusStr))
                {
                    statusTags.Add(new TitledTag(statusStr));
                }

                if (statusTags.Count > 0)
                {
                    AddLine(par, $"{Strings.TagType_Status}: ", statusTags, TagType.Unknown);
                }
            }

            if (item.Details.Tags is TagsContainer[] containers)
            {
                foreach (var container in containers)
                {
                    if (container.Items?.Any() == true)
                    {
                        AddLine(par, $"{container.Title}: ", container.Items.Take(4), container.TagType);
                    }
                }
            }

            infoRichTextBlock.Blocks.Add(par);
        }
    }
}

namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

#if UNO
    using Windows.UI.Text;
#elif WINUI3
    using Microsoft.UI.Text;
#endif
#if WINUI3
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Documents;
#else
    using Windows.UI.Text;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Documents;
#endif

    using FSClient.Localization.Resources;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Humanizer;

    public sealed partial class ChangesDialog : ContentDialog, IContentDialog<ChangesDialogInput, ChangesDialogOutput>
    {
        public ChangesDialog()
        {
            InitializeComponent();
        }

        public Task<ChangesDialogOutput> ShowAsync(ChangesDialogInput arg, CancellationToken cancellationToken)
        {
            return Dispatcher.CheckBeginInvokeOnUI(async () =>
            {
                TextBlock.Inlines.Clear();

                foreach (var entity in arg.Changelog.OrderByDescending(e => e.Version))
                {
                    if ((arg.ShowFromVersion == null
                         || entity.Version >= arg.ShowFromVersion)
                        && (arg.ShowToVersion == null
                            || entity.Version <= arg.ShowToVersion))
                    {
                        TextBlock.Inlines.Add(new Run
                        {
                            Text = entity.Version?.ToString(3) ?? Strings.ChangesDialog_UnknownVersion,
                            FontWeight = FontWeights.Bold
                        });
                        TextBlock.Inlines.Add(new LineBreak());

                        if (entity.Changes != null)
                        {
                            foreach (var change in entity.Changes)
                            {
                                TextBlock.Inlines.Add(new Run {Text = "● " + change});
                                TextBlock.Inlines.Add(new LineBreak());
                            }

                            TextBlock.Inlines.Add(new LineBreak());
                        }
                    }
                }

                if (arg.UpdateVersion == null)
                {
                    Title = Strings.ChangesDialog_TitleLastUpdateChanges.FormatWith(arg.UpdateVersion);
                    PrimaryButtonText = Strings.ContentDialog_Ok;
                    SecondaryButtonText = string.Empty;
                }
                else
                {
                    Title = Strings.ChangesDialog_TitleNewUpdateIsAvailable.FormatWith(arg.UpdateVersion);
                    PrimaryButtonText = Strings.ChangesDialog_UpdateToNewVersion;
                    SecondaryButtonText = Strings.ContentDialog_Cancel;
                }

                return await this.ShowAsync(cancellationToken).ConfigureAwait(true) switch
                {
                    ContentDialogResult.Primary => new ChangesDialogOutput
                    {
                        ShouldOpenUpdatePage = arg.UpdateVersion != null
                    },
                    _ => new ChangesDialogOutput()
                };
            });
        }
    }
}

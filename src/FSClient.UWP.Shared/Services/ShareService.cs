namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.DataTransfer;
    using Windows.Storage.Streams;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Microsoft.Extensions.Logging;

    using File = FSClient.Shared.Models.File;

    public class ShareService : IShareService
    {
        private readonly ILogger logger;

        public ShareService(
            ILogger log)
        {
            logger = log;
            try
            {
                IsSupported = DataTransferManager.IsSupported();
            }
            catch
            {
                IsSupported = false;
            }
        }

        public bool IsSupported { get; }

        public Task<bool> ShareItemAsync(ItemInfo item)
        {
            if (!IsSupported)
            {
                return Task.FromResult(false);
            }

            var manager = DataTransferManager.GetForCurrentView();
            var tcs = new TaskCompletionSource<bool>();

            manager.DataRequested += DataRequested;
            DataTransferManager.ShowShareUI();

            return tcs.Task;

            void DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
            {
                sender.DataRequested -= DataRequested;

                try
                {
                    if (item.Link != null)
                    {
                        args.Request.Data.SetWebLink(item.Link);
                    }

                    var webImage = item.Poster.Count > 0
                        ? item.Poster
                        : item.Details.Images.FirstOrDefault();

                    if (webImage[ImageSize.Original] is Uri bitmapLink)
                    {
                        var rasRef = RandomAccessStreamReference.CreateFromUri(bitmapLink);
                        args.Request.Data.SetBitmap(rasRef);
                    }

                    if (!string.IsNullOrEmpty(item.Details.Description))
                    {
                        args.Request.Data.SetText(item.Details.Description);
                    }

                    if (!string.IsNullOrEmpty(item.Title))
                    {
                        args.Request.Data.Properties.Title = item.Title;
                        args.Request.Data.Properties.Description =
                            string.Format(Strings.ShareService_DataItemDescription, item.Title);
                    }

                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex);
                    args.Request.FailWithDisplayText(Strings.ShareService_UnknownError);
                    tcs.TrySetResult(false);
                }
            }
        }

        public Task<bool> ShareFrameAsync(Stream stream, TimeSpan position, File file)
        {
            if (!IsSupported)
            {
                return Task.FromResult(false);
            }

            var manager = DataTransferManager.GetForCurrentView();
            var tcs = new TaskCompletionSource<bool>();

            manager.DataRequested += DataRequested;
            DataTransferManager.ShowShareUI();

            return tcs.Task;

            void DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
            {
                sender.DataRequested -= DataRequested;

                try
                {
                    var positionStr = position.ToFriendlyString();
                    var fileTitle = (file.Title + " "
                                                + (file.Season.HasValue ? "s" + file.Season.Value : "")
                                                + (file.Episode.HasValue
                                                    ? "e" + file.Episode.Value.ToFormattedString()
                                                    : ""))
                        .Trim() + $" ({positionStr})";


                    if (file.FrameLink != null)
                    {
                        args.Request.Data.SetWebLink(file.FrameLink);
                    }

                    var reference = RandomAccessStreamReference.CreateFromStream(stream.AsRandomAccessStream());
                    args.Request.Data.SetBitmap(reference);
                    args.Request.Data.SetText($"{file.ItemTitle} - {fileTitle}");
                    args.Request.Data.Properties.Title = file.ItemTitle;
                    args.Request.Data.Properties.Description = fileTitle;

                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex);
                    args.Request.FailWithDisplayText(Strings.ShareService_UnknownError);
                    tcs.TrySetResult(false);
                }
            }
        }

        public Task<bool> CopyTextToClipboardAsync(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return DispatcherHelper.GetForCurrentOrMainView().CheckBeginInvokeOnUI(() =>
            {
                try
                {
                    var dataPackage = new DataPackage();
                    dataPackage.SetData(StandardDataFormats.Text, text);

                    Clipboard.SetContent(dataPackage);
                    Clipboard.Flush();

                    return Task.FromResult(true);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex);
                    return Task.FromResult(false);
                }
            });
        }
    }
}

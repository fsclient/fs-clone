namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.ApplicationModel;
    using Windows.ApplicationModel.UserActivities;
    using Windows.Foundation;
    using Windows.Foundation.Metadata;
    using Windows.Graphics.Imaging;
    using Windows.Storage;
    using Windows.Storage.Streams;
    using Windows.UI.Shell;
    using Windows.UI.StartScreen;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;

    using Nito.AsyncEx;

    public class TileService : ITileService
    {
        private const int MaxItemsCountInJumpList = 8;
        private const int MaxErrors = 10;

        private static readonly AsyncLazy<string> adaptiveCardLazy = new AsyncLazy<string>(() =>
            Task.Run(() =>
                System.IO.File.ReadAllText(
                    $@"{Package.Current.InstalledLocation.Path}\Assets\AdaptiveCards\HistoryItemCard.json"))
        );

        private static readonly SemaphoreSlim recentItemsSemaphore = new SemaphoreSlim(1);

        private static readonly bool TimelineAvailable =
            ApiInformation.IsApiContractPresent(typeof(UniversalApiContract).FullName, 5);

        private UserActivityChannel? userActivityChannel;
        private UserActivitySession? userActivitySession;
        private int errorsCount;

        private readonly ILogger logger;

        public TileService(ILogger log)
        {
            logger = log;
        }

        public async Task UpdateTimelineAsync(IEnumerable<ItemInfo> updatedItems, bool isRemoving, CancellationToken cancellationToken)
        {
            if (!TimelineAvailable
                || errorsCount > MaxErrors)
            {
                return;
            }

            updatedItems = updatedItems.Where(item => item?.Key != null);

            try
            {
                await DispatcherHelper.GetForCurrentOrMainView().RunTaskAsync(async () =>
                    {
                        userActivityChannel ??= (userActivityChannel = UserActivityChannel.GetDefault());
                        if (userActivityChannel == null)
                        {
                            return;
                        }

                        if (isRemoving)
                        {
                            foreach (var item in updatedItems)
                            {
                                try
                                {
                                    var activityId = GetActivityId(item);
                                    if (userActivitySession?.ActivityId == activityId)
                                    {
                                        userActivitySession.Dispose();
                                        userActivitySession = null;
                                    }

                                    var deleteOperation = userActivityChannel.DeleteActivityAsync(activityId);
                                    if (deleteOperation != null)
                                    {
                                        await deleteOperation.AsTask(cancellationToken).ConfigureAwait(true);
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    errorsCount++;
                                    ex.Data["ItemInfo"] = item?.ToString();
                                    ex.Data["IsRemoving"] = isRemoving;
                                    logger?.LogWarning(ex);
                                }
                            }
                        }
                        else
                        {
                            UserActivity? userActivity = null;
                            var adaptiveCard = await adaptiveCardLazy;
                            if (string.IsNullOrEmpty(adaptiveCard))
                            {
                                return;
                            }

                            foreach (var item in updatedItems.Where(item => !string.IsNullOrWhiteSpace(item.Title)))
                            {
                                try
                                {
                                    var getActivityOperation =
                                        userActivityChannel.GetOrCreateUserActivityAsync(GetActivityId(item));
                                    var activity = getActivityOperation == null
                                        ? null
                                        : await getActivityOperation.AsTask(cancellationToken).ConfigureAwait(true);
                                    if (activity == null)
                                    {
                                        continue;
                                    }

                                    adaptiveCard = adaptiveCard.Replace("{{title}}", JsonEscape(item.Title));
                                    adaptiveCard = adaptiveCard.Replace("{{description}}",
                                        JsonEscape(item.Details?.Description));
                                    adaptiveCard = adaptiveCard.Replace("{{year}}",
                                        JsonEscape(item.Details?.Year?.ToString()));
                                    adaptiveCard = adaptiveCard.Replace("{{poster}}",
                                        JsonEscape(item.Poster[ImageSize.Preview]?.ToString()));
                                    adaptiveCard = adaptiveCard.Replace("{{genres}}", string.Join(", ",
                                        item.Details?.Tags?
                                            .FirstOrDefault(t => t.TagType == TagType.Genre)?.Items?
                                            .Select(t => JsonEscape(t.Title))
                                        ?? Enumerable.Empty<string>()));

                                    activity.ActivationUri = UriParserHelper.GenerateUriFromItemInfo(item);
                                    activity.VisualElements.DisplayText = item.Title;
                                    activity.VisualElements.Content =
                                        AdaptiveCardBuilder.CreateAdaptiveCardFromJson(adaptiveCard);

                                    var saveOperation = activity.SaveAsync();
                                    if (saveOperation != null)
                                    {
                                        await saveOperation.AsTask(cancellationToken).ConfigureAwait(true);
                                        userActivity = activity;
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    errorsCount++;
                                    ex.Data["ItemInfo"] = item?.ToString();
                                    ex.Data["IsRemoving"] = isRemoving;
                                    logger?.LogWarning(ex);
                                }
                            }

                            userActivitySession?.Dispose();
                            userActivitySession = userActivity?.CreateSession();
                        }
                    })
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                errorsCount++;
                ex.Data["IsRemoving"] = isRemoving;
                logger?.LogWarning(ex);
            }

            static string GetActivityId(ItemInfo item)
            {
                return "FS_HistoryItem_" + item.Key;
            }

            static string JsonEscape(string? input)
            {
                return JsonConvert.ToString(input ?? "").Trim('"');
            }
        }

        public async Task<bool> SetRecentItemsToJumpListAsync(IAsyncEnumerable<ItemInfo> items,
            CancellationToken cancellationToken = default)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            try
            {
                using (await recentItemsSemaphore.LockAsync(cancellationToken).ConfigureAwait(true))
                {
                    if (JumpList.IsSupported())
                    {
                        var list = await JumpList.LoadCurrentAsync();
                        list.Items.Clear();

                        list.Items.Add(JumpListItem.CreateWithArguments(
                            UriParserHelper.GetQueryStringFromViewModel(NavigationPageType.Home),
                            NavigationPageType.Home.GetDisplayName()));
                        list.Items.Add(JumpListItem.CreateWithArguments(
                            UriParserHelper.GetQueryStringFromViewModel(NavigationPageType.Search),
                            NavigationPageType.Search.GetDisplayName()));
                        list.Items.Add(JumpListItem.CreateWithArguments(
                            UriParserHelper.GetQueryStringFromViewModel(NavigationPageType.Favorites),
                            NavigationPageType.Favorites.GetDisplayName()));
                        list.Items.Add(JumpListItem.CreateWithArguments(
                            UriParserHelper.GetQueryStringFromViewModel(NavigationPageType.History),
                            NavigationPageType.History.GetDisplayName()));

                        var trimmedItems = await items.Take(MaxItemsCountInJumpList)
                            .ToArrayAsync(cancellationToken)
                            .ConfigureAwait(false);
                        foreach (var itemInfo in trimmedItems)
                        {
                            if (itemInfo?.Link == null
                                || string.IsNullOrWhiteSpace(itemInfo.Title))
                            {
                                continue;
                            }

                            var item = JumpListItem.CreateWithArguments(
                                QueryStringHelper.CreateQueryString(new Dictionary<string, string?>
                                {
                                    ["type"] = "recentItem",
                                    ["site"] = itemInfo.Site.Value,
                                    ["id"] = itemInfo.SiteId,
                                    ["link"] = itemInfo.Link.ToString()
                                }),
                                itemInfo.Title);
                            item.GroupName = NavigationPageType.History.GetDisplayName();

                            list.Items.Add(item);
                        }

                        await list.SaveAsync().AsTask(cancellationToken).ConfigureAwait(true);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
                return false;
            }
        }

        public async Task<bool> PinItemTileAsync(ItemInfo item, CancellationToken cancellationToken = default)
        {
            var success = false;
            try
            {
                if (item == null
                    || item.Poster.GetOrBigger(ImageSize.Thumb) is not Uri link)
                {
                    return false;
                }

                var fileName = item.SiteId + ".jpg";

                var outputFile = await ApplicationData.Current.LocalFolder
                    .CreateFileAsync(
                        fileName,
                        CreationCollisionOption.ReplaceExisting)
                    .AsTask(cancellationToken)
                    .ConfigureAwait(true);

                IRandomAccessStream? inputStream = null, outputStream = null;
                try
                {
                    var httpClient = new HttpClient();
                    inputStream = (await httpClient.GetBuilder(link)
                            .SendAsync(cancellationToken)
                            .AsStream()
                            .ConfigureAwait(true))?
                        .AsRandomAccessStream();
                    outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);

                    if (inputStream != null
                        && outputStream != null)
                    {
                        await DarkenImageBottom(
                                inputStream,
                                outputStream,
                                true)
                            .ConfigureAwait(true);
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex);
                }
                finally
                {
                    inputStream?.Dispose();
                    outputStream?.Dispose();
                }

                var secondaryTile = new SecondaryTile(
                    "Tile_" + item.SiteId,
                    item.Title,
                    QueryStringHelper.CreateQueryString(new Dictionary<string, string?>
                    {
                        ["type"] = "itemTile",
                        ["site"] = item.Site.Value,
                        ["id"] = item.SiteId,
                        ["link"] = item.Link?.ToString()
                    }),
                    new Uri("ms-appdata:///local/" + fileName),
                    TileSize.Square150x150);
                secondaryTile.VisualElements.ShowNameOnSquare150x150Logo = true;

                success = await secondaryTile.RequestCreateAsync();
                if (!success)
                {
                    await outputFile.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                ex.Data["Item"] = item?.ToString();
                logger?.LogError(ex);
                if (!success)
                {
                    return false;
                }
            }

            return true;
        }

        public async Task CheckSecondatyTilesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var packages = await SecondaryTile.FindAllForPackageAsync().AsTask(cancellationToken)
                    .ConfigureAwait(true);
                var ids = packages
                    .Select(t => t.TileId[5..] + ".jpg")
                    .ToList();
                foreach (var pic in await ApplicationData.Current.LocalFolder.GetFilesAsync())
                {
                    if (!ids.Contains(pic.Name)
                        && pic.FileType == ".jpg")
                    {
                        await pic.DeleteAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }
        }

        // http://www.iter.dk/post/2012/10/08/Using-User-Provided-Images-for-Secondary-Tiles
        private static async Task DarkenImageBottom(IRandomAccessStream input, IRandomAccessStream output, bool square)
        {
            BitmapDecoder? decoder = null;
            byte[]? sourcePixels = null;
            using (var fileStream = input)
            {
                decoder = await BitmapDecoder.CreateAsync(fileStream);
                // Scale image to appropriate size 
                var transform = new BitmapTransform();
                var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation, // This sample ignores Exif orientation 
                    ColorManagementMode.DoNotColorManage
                );
                // An array containing the decoded image data, which could be modified before being displayed 
                sourcePixels = pixelData.DetachPixelData();
            }

            uint width, height;
            if (square)
            {
                height = width = Math.Min(decoder.PixelWidth, decoder.PixelHeight);
            }
            else
            {
                width = decoder.PixelWidth;
                height = decoder.PixelHeight;
            }

            if (decoder != null && sourcePixels != null)
            {
                for (uint col = 0; col < width; col++)
                {
                    for (var row = (uint)(height * .6); row < height; row++)
                    {
                        var idx = ((row * width) + col) * 4;
                        if (decoder.BitmapPixelFormat == BitmapPixelFormat.Bgra8 ||
                            decoder.BitmapPixelFormat == BitmapPixelFormat.Rgba8)
                        {
                            var frac = 1 - Math.Sin(((row / (double)height) - .6) * (1 / .4));
                            var b = sourcePixels[idx];
                            var g = sourcePixels[idx + 1];
                            var r = sourcePixels[idx + 2];
                            sourcePixels[idx] = (byte)(b * frac);
                            sourcePixels[idx + 1] = (byte)(g * frac);
                            sourcePixels[idx + 2] = (byte)(r * frac);
                        }
                    }
                }

                var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);

                enc.SetPixelData(BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    width,
                    height,
                    decoder.DpiX,
                    decoder.DpiY,
                    sourcePixels);

                await enc.FlushAsync();
            }
        }
    }
}

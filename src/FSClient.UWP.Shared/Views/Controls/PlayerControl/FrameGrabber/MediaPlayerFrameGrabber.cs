namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Graphics.Imaging;
    using Windows.Media.Playback;
    using Windows.Storage.Streams;

    using FSClient.UWP.Shared.Helpers;

    using Nito.AsyncEx;

#if UAP
    using Windows.Graphics.Display;

    using Microsoft.Graphics.Canvas;
    using Microsoft.Graphics.Canvas.UI.Xaml;

    using Windows.Foundation.Metadata;
#endif

    /// <summary>
    /// Details
    /// https://github.com/microsoft/microsoft-ui-xaml/issues/2019
    /// </summary>
    public sealed class MediaPlayerFrameGrabber : IFrameGrabber
    {
        public static readonly bool IsSupported =
#if UAP
            ApiInformation.IsTypePresent(typeof(MediaPlayer).FullName)
            && ApiInformation.IsPropertyPresent(typeof(MediaPlayer).FullName, nameof(MediaPlayer.IsVideoFrameServerEnabled));
#else
            false;
#endif

        private readonly MediaPlayer mediaPlayer;
#if UAP
        private readonly CanvasDevice canvasDevice = new CanvasDevice();
#endif
        private readonly TaskCompletionSource<bool> mediaOpenedCTS;

        private BitmapSize? desiredSize;
        private TaskCompletionSource<IRandomAccessStream?>? frameAvailableCTS;

        public MediaPlayerFrameGrabber(
            IMediaPlaybackSource source)
        {
            mediaPlayer = new MediaPlayer();
            mediaPlayer.CommandManager.IsEnabled = false;
            mediaPlayer.SystemMediaTransportControls.IsEnabled = false;
            mediaPlayer.IsVideoFrameServerEnabled = true;
            mediaPlayer.PlaybackSession.PlaybackRate = 0;
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            mediaPlayer.Source = source;

            mediaOpenedCTS = new TaskCompletionSource<bool>();
        }

        private void MediaPlayer_VideoFrameAvailable(MediaPlayer sender, object args)
        {
            if (frameAvailableCTS == null)
            {
                return;
            }

            sender.VideoFrameAvailable -= MediaPlayer_VideoFrameAvailable;

            _ = DispatcherHelper.GetForCurrentOrMainView().CheckBeginInvokeOnUI(async () =>
            {
                if (this.desiredSize is not BitmapSize desiredSize)
                {
                    frameAvailableCTS.TrySetResult(null);
                    return;
                }

                try
                {
#if UAP
                    var stream =
 await GrabFromPlayerMomentAsync(sender, canvasDevice, desiredSize).ConfigureAwait(false);
                    frameAvailableCTS.TrySetResult(stream);
#endif
                }
                catch (Exception ex)
                {
                    frameAvailableCTS.TrySetException(ex);
                }
            });
        }

        private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            mediaOpenedCTS.TrySetResult(true);
        }

        public static async Task<IRandomAccessStream?> GrabFromPlayerMomentAsync(MediaPlayer sender,
            BitmapSize desiredSize)
        {
#if UAP
            using var canvasDevice = new CanvasDevice();
            return await GrabFromPlayerMomentAsync(sender, canvasDevice, desiredSize).ConfigureAwait(false);
#else
            throw new NotSupportedException();
#endif
        }

#if UAP
        private static async Task<IRandomAccessStream?> GrabFromPlayerMomentAsync(MediaPlayer player, CanvasDevice canvasDevice, BitmapSize desiredSize)
        {
            if (desiredSize.Width == 0 || desiredSize.Height == 0)
            {
                throw new InvalidOperationException("Frame Width or Height cannot be zero.");
            }

            var frameServerDest =
 new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)desiredSize.Width, (int)desiredSize.Height, BitmapAlphaMode.Ignore);
            var canvasImageSource =
 new CanvasImageSource(canvasDevice, (int)desiredSize.Width, (int)desiredSize.Height, DisplayInformation.GetForCurrentView().LogicalDpi);

            using var inputBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, frameServerDest);
            using var ds = canvasImageSource.CreateDrawingSession(Windows.UI.Colors.Black);

            player.CopyFrameToVideoSurface(inputBitmap);
            ds.DrawImage(inputBitmap);

            var stream = new InMemoryRandomAccessStream();
            await inputBitmap.SaveAsync(stream, CanvasBitmapFileFormat.Bmp);
            return stream;
        }

#endif

        public async Task<IRandomAccessStream?> GrabAsync(TimeSpan position, BitmapSize desiredSize,
            CancellationToken cancellationToken)
        {
            try
            {
                mediaPlayer.PlaybackSession.Position = position;
                if (mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Opening)
                {
                    await mediaOpenedCTS.Task.WaitAsync(cancellationToken);
                }

                var w = desiredSize.Width;
                var h = desiredSize.Height;
                if (w == 0 && h == 0)
                {
                    h = mediaPlayer.PlaybackSession.NaturalVideoHeight;
                    w = mediaPlayer.PlaybackSession.NaturalVideoWidth;
                }
                else if (w == 0)
                {
                    w = (uint)((double)h / mediaPlayer.PlaybackSession.NaturalVideoHeight *
                               mediaPlayer.PlaybackSession.NaturalVideoWidth);
                }
                else if (h == 0)
                {
                    h = (uint)((double)w / mediaPlayer.PlaybackSession.NaturalVideoWidth *
                               mediaPlayer.PlaybackSession.NaturalVideoHeight);
                }

                w += w % 2;
                h += h % 2;
                this.desiredSize = desiredSize = new BitmapSize {Width = w, Height = h};

                frameAvailableCTS = new TaskCompletionSource<IRandomAccessStream?>();
                mediaPlayer.VideoFrameAvailable += MediaPlayer_VideoFrameAvailable;

                return await frameAvailableCTS.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                mediaPlayer.VideoFrameAvailable -= MediaPlayer_VideoFrameAvailable;
            }
        }

        public void Dispose()
        {
            mediaPlayer.Dispose();
#if UAP
            canvasDevice.Dispose();
#endif
        }
    }
}

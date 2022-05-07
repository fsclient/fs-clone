namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Graphics.Imaging;
    using Windows.Media.Editing;
    using Windows.Storage;
    using Windows.Storage.Streams;

    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

    public sealed class MediaClipFrameGrabber : IFrameGrabber
    {
        private readonly AsyncLazy<MediaComposition?> lazyMediaClip;
        private readonly ILogger logger;

        public MediaClipFrameGrabber(StorageFile storageFile, ILogger logger)
        {
            this.logger = logger;
            lazyMediaClip = new AsyncLazy<MediaComposition?>(() => CreateMediaComposition(storageFile),
                AsyncLazyFlags.ExecuteOnCallingThread);
        }

        public async Task<IRandomAccessStream?> GrabAsync(TimeSpan position, BitmapSize desiredSize,
            CancellationToken cancellationToken)
        {
            try
            {
                var composition = await lazyMediaClip;
                if (composition == null
                    || position >= composition.Duration
                    || position < TimeSpan.Zero)
                {
                    return null;
                }

                return await composition.GetThumbnailAsync(position, (int)desiredSize.Width, (int)desiredSize.Height,
                    VideoFramePrecision.NearestFrame).AsTask(cancellationToken);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex);
                return null;
            }
        }

        public void Dispose()
        {
        }

        private async Task<MediaComposition?> CreateMediaComposition(StorageFile storageFile)
        {
            try
            {
                var clip = await MediaClip.CreateFromFileAsync(storageFile);
                return new MediaComposition {Clips = {clip}};
            }
            // IO
            catch (ArgumentException ex) when (ex.Message.Contains("Error creating clip from file"))
            {
                logger.LogWarning(ex);
                return null;
            }
        }
    }
}

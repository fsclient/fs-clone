namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Graphics.Imaging;
    using Windows.Storage.Streams;

    public interface IFrameGrabber : IDisposable
    {
        Task<IRandomAccessStream?> GrabAsync(TimeSpan position, BitmapSize desiredSize,
            CancellationToken cancellationToken);
    }
}

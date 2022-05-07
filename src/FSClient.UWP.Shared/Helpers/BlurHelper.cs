namespace FSClient.UWP.Shared.Helpers
{
    using System;
    using System.Threading.Tasks;

    using Windows.Storage.Streams;
#if WINUI3
    using Microsoft.UI.Xaml.Media.Imaging;
#else
    using Windows.UI.Xaml.Media.Imaging;
#endif

    public static class BlurHelper
    {
        public static async Task<BitmapImage?> BlurFromUri(Uri? uri, float blurAmount = 2.0f)
        {
            if (uri == null)
            {
                return null;
            }

            try
            {
                var image = new BitmapImage();

#if WINUI3
                var stream = await RandomAccessStreamReference.CreateFromUri(uri).OpenReadAsync();
                await image.SetSourceAsync(stream);
#else
                var canvasDevice = new Microsoft.Graphics.Canvas.CanvasDevice();
                var bitmap = await Microsoft.Graphics.Canvas.CanvasBitmap.LoadAsync(canvasDevice, uri);

                using var renderer = new Microsoft.Graphics.Canvas.CanvasRenderTarget(canvasDevice,
                                                      bitmap.SizeInPixels.Width,
                                                      bitmap.SizeInPixels.Height,
                                                      bitmap.Dpi);

                using (var ds = renderer.CreateDrawingSession())
                {
                    var blur = new Microsoft.Graphics.Canvas.Effects.GaussianBlurEffect
                    {
                        BlurAmount = blurAmount,
                        Source = bitmap
                    };
                    ds.DrawImage(blur);
                }

                var stream = new InMemoryRandomAccessStream();
                await renderer.SaveAsync(stream, Microsoft.Graphics.Canvas.CanvasBitmapFileFormat.Png);
                await image.SetSourceAsync(stream);
#endif

                return image;
            }
            catch
            {
                return new BitmapImage {UriSource = uri};
            }
        }
    }
}

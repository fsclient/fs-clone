namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Threading.Tasks;

    using Windows.UI;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Popup = Microsoft.UI.Xaml.Controls.Primitives.Popup;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;
    using Popup = Windows.UI.Xaml.Controls.Primitives.Popup;
#endif

    using FSClient.UWP.Shared.Helpers;
    public static class ImageViewer
    {
        private static readonly Popup popup;
        private static readonly Image image;
        private static readonly ScrollViewer scrollViewer;

        static ImageViewer()
        {
            popup = new Popup {IsLightDismissEnabled = true};

            var grid = new Grid {Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0))};
            grid.Tapped += Grid_Tapped;
            popup.Child = grid;

            scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollMode = ScrollMode.Disabled,
                MaxZoomFactor = 3,
                MinZoomFactor = 1,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Disabled,
                ZoomMode = ZoomMode.Enabled,
                Width = Window.Current.Bounds.Width,
                Height = Window.Current.Bounds.Height
            };
            scrollViewer.PointerWheelChanged += ImageViewerWheelZoom;
            grid.Children.Add(scrollViewer);

            var renderTransform = new CompositeTransform();
            image = new Image
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ManipulationMode =
                    ManipulationModes.TranslateX | ManipulationModes.TranslateY | ManipulationModes.System,
                Stretch = Stretch.Uniform,
                RenderTransform = renderTransform
            };
            image.ManipulationDelta += ImageViewerMove;

            scrollViewer.Content = image;

            image.ImageOpened += (s, a) => EnsureScrollViewerZoom(image.Source as BitmapImage);

            popup.Opened += (s, a) => EnsureScrollViewerZoom(image.Source as BitmapImage);
        }

        public static bool IsOpened => popup?.IsOpen ?? false;

        public static void Show(ImageSource src)
        {
            image.Source = src;
            EnsureScrollViewerZoom(image.Source as BitmapImage);
            popup.IsOpen = true;
        }

        public static void Close()
        {
            popup.IsOpen = false;
        }

        private static void EnsureScrollViewerZoom(BitmapImage? bitmapImage)
        {
            if (image.RenderTransform is CompositeTransform compositeTransform)
            {
                compositeTransform.TranslateX = compositeTransform.TranslateY = 0;
            }

            scrollViewer.Width = Window.Current.Bounds.Width;
            scrollViewer.Height = Window.Current.Bounds.Height;

            scrollViewer.MinZoomFactor = GetZoomFactorFromImage(bitmapImage);
            scrollViewer.MaxZoomFactor = scrollViewer.MinZoomFactor * 3;
            scrollViewer.ChangeView(0, 0, scrollViewer.MinZoomFactor);
        }

        private static float GetZoomFactorFromImage(BitmapImage? bitmapImage)
        {
            if (bitmapImage != null
                && bitmapImage.PixelHeight > 0
                && bitmapImage.PixelWidth > 0)
            {
                var heightRatio = Window.Current.Bounds.Height / bitmapImage.PixelHeight;
                var widthRatio = Window.Current.Bounds.Width / bitmapImage.PixelWidth;
                var minRation = Math.Min((float)Math.Min(heightRatio, widthRatio), 1f);
                return Math.Max(minRation, 0.1f);
            }

            return 1;
        }

        private static void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender.GetType() == e.OriginalSource.GetType())
            {
                popup.IsOpen = false;
            }
        }

        private static void ImageViewerMove(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image?.RenderTransform is not CompositeTransform trasform)
            {
                return;
            }

            trasform.TranslateX += e.Delta.Translation.X;
            trasform.TranslateY += e.Delta.Translation.Y;
        }

        private static async void ImageViewerWheelZoom(object sender, PointerRoutedEventArgs e)
        {
            var scroll = (ScrollViewer)sender;
            await scroll.Dispatcher.CheckBeginInvokeOnUI(
                () =>
                {
                    var point = e.GetCurrentPoint(scroll);
                    var pos = point.Position;
                    var wheel = point.Properties.MouseWheelDelta / 200f;
                    scroll.ChangeView(
                        scroll.HorizontalOffset + pos.X,
                        scroll.VerticalOffset + pos.Y,
                        scroll.ZoomFactor + wheel);
                    e.Handled = true;
                });
        }
    }
}

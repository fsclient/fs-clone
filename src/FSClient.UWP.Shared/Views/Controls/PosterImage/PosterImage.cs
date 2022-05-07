namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Linq;
    using System.Diagnostics.CodeAnalysis;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Media.Imaging;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;
#endif

    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    public class PosterImage : ContentControl
    {
        private Grid? rootGrid;
        private (WebImage webImage, Image? preview, Image? preferred)? previousWebImage;

        public PosterImage()
        {
            DefaultStyleKey = nameof(PosterImage);
            Loaded += PosterImage_Loaded;
        }

        public event ExceptionRoutedEventHandler? ImageFailed;

        public event RoutedEventHandler? ImageOpened;

        public bool ForcePosterAspectRatio
        {
            get => (bool)GetValue(ForcePosterAspectRatioProperty);
            set => SetValue(ForcePosterAspectRatioProperty, value);
        }

        public static readonly DependencyProperty ForcePosterAspectRatioProperty =
            DependencyProperty.Register(nameof(ForcePosterAspectRatio), typeof(bool), typeof(PosterImage),
                new PropertyMetadata(false));

        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(PosterImage),
                new PropertyMetadata(Stretch.None, StretchChanged));

        private static void StretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var posterImage = (PosterImage)d;
            if (posterImage?.rootGrid == null)
            {
                return;
            }

            foreach (var image in posterImage.rootGrid.Children.OfType<Image>())
            {
                image.Stretch = (Stretch)e.NewValue;
            }
        }

        public WebImage Source
        {
            get => (WebImage)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(WebImage), typeof(PosterImage),
                new PropertyMetadata(null, SourceChanged));

        public ImageSize PreferredSize
        {
            get => (ImageSize)GetValue(PreferredSizeProperty);
            set => SetValue(PreferredSizeProperty, value);
        }

        public static readonly DependencyProperty PreferredSizeProperty =
            DependencyProperty.Register(nameof(PreferredSize), typeof(ImageSize), typeof(PosterImage),
                new PropertyMetadata(ImageSize.Preview));

        public bool Expandable
        {
            get => (bool)GetValue(ExpandableProperty);
            set => SetValue(ExpandableProperty, value);
        }

        public static readonly DependencyProperty ExpandableProperty =
            DependencyProperty.Register(nameof(Expandable), typeof(bool), typeof(PosterImage),
                new PropertyMetadata(false, ExpandablePropertyChanged));

        private static void ExpandablePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var posterImage = (PosterImage)d;
            posterImage.Tapped -= posterImage.ToggleImageViewer;
            if ((e.NewValue as bool?) == true)
            {
                posterImage.Tapped += posterImage.ToggleImageViewer;
            }
        }

        private static async void SourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PosterImage)d;
            await control.WaitForLoadedAsync();

            if (control.rootGrid == null)
            {
                return;
            }

            if (e.NewValue is WebImage webImage
                && webImage.Count > 0)
            {
                if (control.previousWebImage is { } previousWebImage
                    && webImage.Values.SequenceEqual(previousWebImage.webImage.Values))
                {
                    if (e.OldValue == null
                        && !control.rootGrid.Children.Any())
                    {
                        if (previousWebImage.preview != null)
                        {
                            control.rootGrid.Children.Add(previousWebImage.preview);
                        }
                        if (previousWebImage.preferred != null)
                        {
                            control.rootGrid.Children.Add(previousWebImage.preferred);
                        }
                    }

                    return;
                }

                var preferredSize = control.PreferredSize;
                if (preferredSize <= ImageSize.Preview
                    && webImage.TryGetAnyValue(preferredSize, out var link)
                    && TryCreateBitmapImage(link, out var source))
                {
                    var image = new Image
                    {
                        Source = source
                    };
                    image.ImageFailed += ImageFailed;
                    image.ImageOpened += ImageOpened;

                    control.rootGrid.Children.Clear();
                    control.rootGrid.Children.Add(image);

                    control.previousWebImage = (webImage, image, null);
                }
                else
                {
                    control.rootGrid.Children.Clear();
                    Image? previewImage = null, preferredImage = null;

                    if (webImage.TryGetAnyValue(preferredSize, out var previewLink)
                        && TryCreateBitmapImage(previewLink, out var previewSource))
                    {
                        previewImage = new Image
                        {
                            Source = previewSource
                        };
                        control.rootGrid.Children.Add(previewImage);
                        previewImage.ImageFailed += ImageFailed;
                        previewImage.ImageOpened += ImageOpened;
                    }

                    if (webImage.TryGetAnyValue(preferredSize, out var preferredLink)
                        && preferredLink != previewLink
                        && TryCreateBitmapImage(preferredLink, out var preferredSource))
                    {
                        preferredImage = new Image
                        {
                            Source = preferredSource
                        };
                        control.rootGrid.Children.Add(preferredImage);
                        preferredImage.ImageFailed += ImageFailed;
                        preferredImage.ImageOpened += ImageOpened;
                        preferredImage.ImageOpened += (s, a) =>
                        {
                            if (control == null
                                || !control.rootGrid.Children.Contains(previewImage))
                            {
                                return;
                            }

                            control.rootGrid.Children.Remove(previewImage);
                        };
                    }

                    control.previousWebImage = (webImage, previewImage, preferredImage);
                }
            }
            else
            {
                control.rootGrid.Children.Clear();
            }

            void ImageOpened(object sender, RoutedEventArgs args)
            {
                if (control?.rootGrid == null
                    || !control.rootGrid.Children.Contains(sender as UIElement))
                {
                    return;
                }
                control.ImageOpened?.Invoke(control, args);
            }
            void ImageFailed(object sender, ExceptionRoutedEventArgs args)
            {
                if (control?.rootGrid == null
                    || !control.rootGrid.Children.Contains(sender as UIElement))
                {
                    return;
                }
                control.ImageFailed?.Invoke(control, args);
            }
        }

        private void PosterImage_Loaded(object sender, RoutedEventArgs e)
        {
            if (Content != null)
            {
                return;
            }

            if (ForcePosterAspectRatio)
            {
                Content = new Viewbox
                {
                    Stretch = Stretch.UniformToFill,
                    Child = rootGrid = new Grid
                    {
                        Width = 151,
                        Height = 220d
                    }
                };
            }
            else
            {

                Content = rootGrid = new Grid();
            }
        }

        private void ToggleImageViewer(object sender, RoutedEventArgs e)
        {
            if (!Expandable
                || rootGrid?.Children.OfType<Image>().LastOrDefault() is not Image imageControl)
            {
                return;
            }

            if (Source.GetOrBigger(ImageSize.Original) is Uri image
                && image.IsAbsoluteUri
                && image != (imageControl.Source as BitmapImage)?.UriSource
                && TryCreateBitmapImage(image, out var bitmapImage))
            {
                ImageViewer.Show(bitmapImage);
            }
            else
            {
                ImageViewer.Show(imageControl.Source);
            }
        }

        private static bool TryCreateBitmapImage(Uri? link, [NotNullWhen(true)] out BitmapImage? bitmapImage)
        {
            if (link == null
                || !link.IsAbsoluteUri)
            {
                bitmapImage = null;
                return false;
            }

            try
            {
                bitmapImage = new BitmapImage(link);
                return true;
            }
            catch (Exception ex)
            {
                bitmapImage = null;

                Logger.Instance.LogWarning(ex);

                return false;
            }
        }
    }
}

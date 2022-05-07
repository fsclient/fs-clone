namespace FSClient.Shared.Models
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public enum ImageSize
    {
        Thumb = 1,
        Preview,
        Original
    }

    public struct WebImage : IEnumerable<(ImageSize Size, Uri Value)>, IEquatable<WebImage>
    {
        private Uri? thumbLink;
        private Uri? previewLink;
        private Uri? originalLink;

        public Uri? this[ImageSize key]
        {
            get => GetOrSmaller(key);
            set => Set(key, value);
        }

        public IEnumerable<ImageSize> Keys => this.Select(p => p.Size);

        public IEnumerable<Uri> Values => this.Select(p => p.Value);

        public int Count => (thumbLink != null ? 1 : 0) + (previewLink != null ? 1 : 0) + (originalLink != null ? 1 : 0);

        public bool ContainsKey(ImageSize key)
        {
            return this.Any(s => s.Size == key);
        }

        public bool TryGetAnyValue(ImageSize key, out Uri? value)
        {
            return (value = GetOrSmaller(key) ?? GetOrBigger(key)) != null;
        }

        public Uri? GetOrSmaller(ImageSize size)
        {
            return this.Reverse().FirstOrDefault(s => s.Size <= size).Value;
        }

        public Uri? GetOrBigger(ImageSize size)
        {
            return this.FirstOrDefault(s => s.Size >= size).Value;
        }

        public void Set(ImageSize key, Uri? value)
        {
            if (value == null)
            {
                return;
            }
            switch (key)
            {
                case ImageSize.Original:
                    originalLink = value;
                    break;
                case ImageSize.Preview:
                    previewLink = value;
                    break;
                case ImageSize.Thumb:
                    thumbLink = value;
                    break;
            }
        }

        public IEnumerator<(ImageSize Size, Uri Value)> GetEnumerator()
        {
            if (thumbLink != null)
            {
                yield return (ImageSize.Thumb, thumbLink);
            }

            if (previewLink != null)
            {
                yield return (ImageSize.Preview, previewLink);
            }

            if (originalLink != null)
            {
                yield return (ImageSize.Original, originalLink);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Equals(WebImage other)
        {
            return (originalLink, previewLink, thumbLink) == (other.originalLink, other.previewLink, other.thumbLink);
        }

        public override bool Equals(object obj)
        {
            return obj is WebImage webImage && Equals(webImage);
        }

        public override int GetHashCode()
        {
            return (originalLink, previewLink, thumbLink).GetHashCode();
        }

        public static bool operator ==(WebImage webImage1, WebImage webImage2)
        {
            return webImage1.Equals(webImage2);
        }

        public static bool operator !=(WebImage webImage1, WebImage webImage2)
        {
            return !(webImage1 == webImage2);
        }

        public static implicit operator WebImage(Uri? input)
        {
            return new WebImage
            {
                [ImageSize.Preview] = input
            };
        }
    }
}

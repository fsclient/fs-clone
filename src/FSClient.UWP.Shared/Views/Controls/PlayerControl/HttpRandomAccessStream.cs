namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Foundation;
    using Windows.Storage.Streams;
    using Windows.Web.Http;

    public sealed class HttpRandomAccessStream : IRandomAccessStreamWithContentType
    {
        private readonly HttpClient client;
        private IInputStream? inputStream;
        private ulong size;
        private string? etagHeader;
        private string? lastModifiedHeader;
        private readonly Uri requestedUri;

        private HttpRandomAccessStream(HttpClient client, Uri uri)
        {
            this.client = client;
            requestedUri = uri;
            Position = 0;
        }

        public ulong Position { get; private set; }

        public ulong Size
        {
            get => size;
            set => throw new NotSupportedException();
        }

        public string ContentType { get; private set; } = string.Empty;

        public bool CanRead => true;

        public bool CanWrite => false;

        public static IAsyncOperation<HttpRandomAccessStream> CreateAsync(HttpClient client, Uri uri)
        {
            var randomStream = new HttpRandomAccessStream(client, uri);

            return AsyncInfo.Run(async cancellationToken =>
            {
                await randomStream.SendRequesAsync(cancellationToken).ConfigureAwait(false);
                return randomStream;
            });
        }

        private async Task SendRequesAsync(CancellationToken cancellationToken)
        {
            Debug.Assert(inputStream == null);

            var request = new HttpRequestMessage(HttpMethod.Get, requestedUri);

            request.Headers.Add("Range", string.Format("bytes={0}-", Position));

            if (!string.IsNullOrEmpty(etagHeader))
            {
                request.Headers.Add("If-Match", etagHeader);
            }

            if (!string.IsNullOrEmpty(lastModifiedHeader))
            {
                request.Headers.Add("If-Unmodified-Since", lastModifiedHeader);
            }

            var response = await client.SendRequestAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead).AsTask(cancellationToken).ConfigureAwait(false);

            if (response.Content.Headers.ContentType != null)
            {
                ContentType = response.Content.Headers.ContentType.MediaType;
            }

            size = response.Content.Headers.ContentLength ?? 0;

            if (response.StatusCode != HttpStatusCode.PartialContent && Position != 0)
            {
                throw new System.Net.Http.HttpRequestException(
                    "HTTP server did not reply with a '206 Partial Content' status.");
            }

            if (!response.Headers.ContainsKey("Accept-Ranges")
                && response.Content.Headers.ContentRange == null)
            {
                throw new System.Net.Http.HttpRequestException(
                    "HTTP server does not support range requests: http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.5");
            }

            if (string.IsNullOrEmpty(etagHeader) && response.Headers.ContainsKey("ETag"))
            {
                etagHeader = response.Headers["ETag"];
            }

            if (string.IsNullOrEmpty(lastModifiedHeader) && response.Content.Headers.ContainsKey("Last-Modified"))
            {
                lastModifiedHeader = response.Content.Headers["Last-Modified"];
            }

            if (response.Content.Headers.ContainsKey("Content-Type"))
            {
                ContentType = response.Content.Headers["Content-Type"];
            }

            inputStream = await response.Content.ReadAsInputStreamAsync().AsTask().ConfigureAwait(false);
        }

        public IRandomAccessStream CloneStream()
        {
            // If there is only one MediaPlayerElement using the stream, it is safe to return itself.
            return this;
        }

        public IInputStream GetInputStreamAt(ulong position)
        {
            throw new NotSupportedException();
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            throw new NotSupportedException();
        }

        public void Seek(ulong position)
        {
            if (Position != position)
            {
                if (inputStream != null)
                {
                    inputStream.Dispose();
                    inputStream = null;
                }

                Position = position;
            }
        }

        public void Dispose()
        {
            if (inputStream != null)
            {
                inputStream.Dispose();
                inputStream = null;
            }
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count,
            InputStreamOptions options)
        {
            return AsyncInfo.Run<IBuffer, uint>(async (cancellationToken, progress) =>
            {
                progress.Report(0);

                try
                {
                    if (inputStream == null)
                    {
                        await SendRequesAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    throw;
                }

                // inputStream shouldn't be null after SendRequesAsync
                var result = await inputStream!.ReadAsync(buffer, count, options).AsTask(cancellationToken, progress)
                    .ConfigureAwait(false);

                Position += result.Length;

                return result;
            });
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            throw new NotSupportedException();
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            throw new NotSupportedException();
        }
    }
}

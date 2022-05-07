namespace FSClient.Shared.Services
{
    using System;

    public class RemoteLaunchDialogInput
    {
        public RemoteLaunchDialogInput(
            Uri? fsProtocolSource,
            Uri? uriSource,
            bool uriSourceIsVideo,
            object? mediaSource)
        {
            FSProtocolSource = fsProtocolSource;
            UriSource = uriSource;
            UriSourceIsVideo = uriSourceIsVideo;
            MediaSource = mediaSource;
        }

        public Uri? FSProtocolSource { get; }

        public Uri? UriSource { get; }

        public bool UriSourceIsVideo { get; }

        public object? MediaSource { get; }
    }
}

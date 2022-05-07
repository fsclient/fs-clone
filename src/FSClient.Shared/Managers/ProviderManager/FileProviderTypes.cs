namespace FSClient.Shared.Managers
{
    using System;

    [Flags]
    public enum FileProviderTypes
    {
        Online = 1,
        Torrent = 2,
        Trailer = 4
    }
}

namespace FSClient.Shared
{
    public static partial class Secrets
    {
        public static string? AppInternalKey { get; }

        public static string? AppInternalIV { get; }

        public static string? AppCenterKey { get; }

        public static string? SupportEmailAddress { get; }

        public static string? TMDbApiKey { get; }

        public static string? KinoPubClient { get; }
        public static string? KinoPubApiKey { get; }

        public static string? KodikApiKey { get; }

        public static string? HDVBApiKey { get; }

        public static string? CollapsApiKey { get; }

        public static string? VideoCDNApiKey { get; }

        public static string? BazonApiKey { get; }

        public static string? UStoreApiKey { get; }

        public static string? LookbaseApiKey { get; }

        public static partial class Test
        {
            public static string? ExFSUserName { get; }
            public static string? ExFSPassword { get; }

            public static string? SeasonVarUserName { get; }
            public static string? SeasonVarPassword { get; }

            public static string? FilmixUserName { get; }
            public static string? FilmixPassword { get; }

            public static string? SAUserName { get; }
            public static string? SAPassword { get; }
        }
    }
}

namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Generic;

    public class ChangelogEntity
    {
        public Version? Version { get; init; }

        public bool? ShowOnStartup { get; init; } = true;

        public IEnumerable<string>? Changes { get; init; }
    }
}

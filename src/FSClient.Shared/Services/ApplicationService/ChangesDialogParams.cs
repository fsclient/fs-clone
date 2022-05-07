namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Generic;

    public record ChangesDialogInput(IEnumerable<ChangelogEntity> Changelog)
    {
        public Version? UpdateVersion { get; init; }

        public Version? ShowFromVersion { get; init; }

        public Version? ShowToVersion { get; init; }
    }

    public class ChangesDialogOutput
    {
        public bool ShouldOpenUpdatePage { get; init; }
    }
}

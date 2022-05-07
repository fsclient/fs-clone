namespace FSClient.UWP.Shared.Views.Controls
{
    public readonly struct MediaRangedProgressBarRange
    {
        public MediaRangedProgressBarRange(double start, double end)
        {
            if (start < 0)
            {
                start = 0;
            }
            else if (start > 100)
            {
                start = 100;
            }

            if (end < 0)
            {
                end = 0;
            }
            else if (end > 100)
            {
                end = 100;
            }

            Start = start;
            End = end;
        }

        public double Start { get; }

        public double End { get; }
    }
}

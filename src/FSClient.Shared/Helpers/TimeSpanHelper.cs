namespace FSClient.Shared.Helpers
{
    using System;
    using System.Text;

    public static class TimeSpanHelper
    {
        public static string ToFriendlyString(this TimeSpan timespan, bool withSign = true, bool forceHours = false)
        {
            if (withSign)
            {
                timespan = TimeSpan.FromSeconds(Math.Round(timespan.TotalSeconds));

                var sb = new StringBuilder();

                if (timespan.TotalSeconds > 0)
                {
                    sb.Append('+');
                }
                else if (timespan.TotalSeconds < 0)
                {
                    sb.Append('-');
                }

                if (Math.Abs(timespan.TotalHours) >= 1 || forceHours)
                {
                    sb.AppendFormat("{0:h\\:mm\\:ss}", timespan);
                }
                else
                {
                    sb.AppendFormat("{0:mm\\:ss}", timespan);
                }

                return sb.ToString();
            }

            if (Math.Abs(timespan.TotalHours) >= 1 || forceHours)
            {
                return timespan.ToString("h\\:mm\\:ss");
            }

            return timespan.ToString("mm\\:ss");
        }
    }
}

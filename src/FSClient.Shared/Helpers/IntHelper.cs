namespace FSClient.Shared.Helpers
{
    using System;

    public static class IntHelper
    {
        public static int DigitsCount(this int n)
        {
            return n == 0 ? 1 : 1 + (int)Math.Log10(Math.Abs(n));
        }
    }
}

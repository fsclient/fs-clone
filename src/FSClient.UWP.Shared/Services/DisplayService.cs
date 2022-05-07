namespace FSClient.UWP.Shared.Services
{
    using System.Threading;

    using Windows.System.Display;

    public class DisplayService
    {
        private readonly DisplayRequest dispRequest = new DisplayRequest();
        private int requestCount;

        public bool IsActive => requestCount > 0;

        public void HoldActive()
        {
            if (requestCount > 0)
            {
                return;
            }

            dispRequest.RequestActive();

            Interlocked.Increment(ref requestCount);
        }

        public void ReleaseActive()
        {
            while (requestCount > 0)
            {
                dispRequest.RequestRelease();

                Interlocked.Decrement(ref requestCount);
            }
        }

        ~DisplayService()
        {
            ReleaseActive();
        }
    }
}

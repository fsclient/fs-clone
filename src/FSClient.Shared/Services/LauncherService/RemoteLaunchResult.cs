namespace FSClient.Shared.Services
{
    public class RemoteLaunchDialogOutput
    {
        public RemoteLaunchDialogOutput(bool isSuccess, bool isCanceled, string? error)
        {
            IsSuccess = isSuccess;
            IsCanceled = isCanceled;
            Error = error;
        }

        public bool IsSuccess { get; }
        public bool IsCanceled { get; }
        public string? Error { get; }
    }
}

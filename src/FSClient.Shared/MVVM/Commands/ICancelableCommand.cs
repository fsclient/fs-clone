namespace FSClient.Shared.Mvvm
{
    using System.Windows.Input;

    public interface ICancelableCommand : ICommand
    {
        ICommand CancelCommand { get; }

        void Cancel();
    }
}

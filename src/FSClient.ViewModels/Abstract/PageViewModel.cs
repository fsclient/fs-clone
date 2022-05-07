namespace FSClient.ViewModels.Abstract
{
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Mvvm;

    public abstract class PageViewModel : ViewModelBase
    {
        protected PageViewModel()
        {
            UpdateCommand = new AsyncCommand<bool>(UpdateAsync, AsyncCommandConflictBehaviour.Skip);
        }

        public AsyncCommand<bool> UpdateCommand { get; }

        public abstract string Caption { get; }

        protected abstract Task UpdateAsync(bool force, CancellationToken cancellationToken);
    }
}

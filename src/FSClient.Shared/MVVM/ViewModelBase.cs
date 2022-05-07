namespace FSClient.Shared.Mvvm
{
    public abstract class ViewModelBase : BindableBase
    {
        public Settings Settings => Settings.Instance;

        public bool ShowProgress
        {
            get => Get<bool>();
            protected set => Set(value);
        }
    }
}

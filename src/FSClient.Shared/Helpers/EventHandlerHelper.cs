namespace FSClient.Shared.Helpers
{
    using System;

    public class EventArgs<TField> : EventArgs
    {
        public EventArgs(TField argument)
        {
            Argument = argument;
        }

        public TField Argument { get; }
    }
}

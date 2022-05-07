namespace FSClient.Shared.Repositories
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Database context.
    /// </summary>
    public interface IDatabaseContext : IDisposable
    {
        /// <summary>
        /// Drop database
        /// </summary>
        ValueTask DropAsync();

        /// <summary>
        /// Checkpoint database context instance.
        /// </summary>
        ValueTask CheckpointAsync();
    }
}

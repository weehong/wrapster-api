namespace Wrapsfer.Domain.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards all pending tracked changes on the shared DbContext without saving them.
    /// Callers must use this after a failure return that followed a partial entity mutation,
    /// so a later SaveChangesAsync on the same scoped context (e.g. by the caller's own
    /// failure-handling save) cannot flush the half-applied work.
    /// </summary>
    void ClearChangeTracker();
}

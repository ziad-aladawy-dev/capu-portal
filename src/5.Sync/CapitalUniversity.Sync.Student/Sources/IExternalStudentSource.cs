using CapitalUniversity.Sync.Student.Domain;

namespace CapitalUniversity.Sync.Student.Sources;

/// <summary>
/// Abstracts the external university system. The default in-memory implementation
/// is registered for Phase 5; a real HTTP/SQL client will replace it in production
/// without changing the rest of the module.
/// </summary>
public interface IExternalStudentSource
{
    IAsyncEnumerable<ExternalStudent> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        CancellationToken cancellationToken);
}
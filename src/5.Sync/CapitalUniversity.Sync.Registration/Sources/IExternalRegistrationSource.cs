using CapitalUniversity.Sync.Registration.Domain;

namespace CapitalUniversity.Sync.Registration.Sources;

/// <summary>
/// Module-supplied upstream source of registration changes. Implementations
/// translate the incoming cursor into a server-side <c>ExternalUpdatedAt &gt;
/// since</c> filter. The host swaps the in-memory simulator for an HTTP-backed
/// implementation when <c>Sync:Integration:UseHttpAdapters</c> is enabled.
/// </summary>
public interface IExternalRegistrationSource
{
    IAsyncEnumerable<ExternalRegistration> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        CancellationToken cancellationToken);
}

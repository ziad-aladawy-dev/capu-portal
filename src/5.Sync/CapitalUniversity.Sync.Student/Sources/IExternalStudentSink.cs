using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Student.Domain;

namespace CapitalUniversity.Sync.Student.Sources;

/// <summary>
/// Push counterpart to <see cref="IExternalStudentSource"/>. Sends a single outbound
/// record to the external university system. The default in-memory implementation
/// is registered for Phase 6; a real HTTP/SOAP client replaces it in production
/// without touching the rest of the module.
///
/// <para>
/// <b>Idempotency contract (REQUIRED).</b> The outbox flow is at-least-once: a
/// successful <see cref="PushAsync"/> followed by a SaveChanges failure on the
/// sync side will be re-pushed by the next tick. Implementations MUST therefore
/// dedup on the <paramref name="idempotencyKey"/> the caller supplies — the
/// outbox writer passes the outbox row's stable <c>Id</c>, which is generated
/// once at outbox-write time and never changes across re-pushes of the same
/// row. A real HTTP sink forwards this as the standard
/// <c>Idempotency-Key</c> header (Stripe / Twilio / AWS / etc. all honour it);
/// the in-memory sink tracks seen keys in-process.
/// </para>
///
/// <para>
/// Returning normally means the external system has accepted the payload (or
/// recognized the key as already-accepted and treated this call as a no-op).
/// Throwing surfaces a transient or terminal failure; the pipeline will report
/// it through <c>SyncResult</c>, the outbox row's <c>AttemptCount</c> is bumped
/// and <c>LastError</c> recorded, and the row remains
/// <see cref="OutboxStatus.Pending"/> so the next tick re-attempts. Hangfire's
/// own retry policy continues to wrap the whole pipeline as before.
/// </para>
/// </summary>
public interface IExternalStudentSink
{
    /// <param name="payload">The outbound state snapshot.</param>
    /// <param name="idempotencyKey">
    /// Stable per-outbox-row identifier. Sink MUST treat a repeat call with
    /// the same key as a no-op (return success without re-running the side
    /// effect). The outbox writer passes <c>StudentOutboxEntity.Id</c>.
    /// </param>
    /// <param name="cancellationToken">Honoured between any I/O hops.</param>
    Task PushAsync(ExternalStudent payload, string idempotencyKey, CancellationToken cancellationToken);
}

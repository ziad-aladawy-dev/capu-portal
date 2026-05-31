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
/// Implementations MUST be idempotent on <see cref="ExternalStudent.ExternalStudentId"/>:
/// the push pipeline can replay an already-accepted payload (Hangfire retry between
/// external accept and outbox-row mark-as-processed, host crash mid-batch, etc.).
/// </para>
///
/// <para>
/// Returning normally means the external system has accepted the payload as the
/// new authoritative state. Throwing surfaces a transient or terminal failure; the
/// pipeline will report it through <c>SyncResult</c>, the outbox row's
/// <c>AttemptCount</c> is bumped and <c>LastError</c> recorded, and the row remains
/// <see cref="OutboxStatus.Pending"/> so the next tick re-attempts. Hangfire's
/// own retry policy continues to wrap the whole pipeline as before.
/// </para>
/// </summary>
public interface IExternalStudentSink
{
    Task PushAsync(ExternalStudent payload, CancellationToken cancellationToken);
}
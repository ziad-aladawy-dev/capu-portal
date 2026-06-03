using CapitalUniversity.Sync.Staff.Domain;

namespace CapitalUniversity.Sync.Staff.Sources;

/// <summary>
/// Push counterpart to <see cref="IExternalStaffSource"/>. Identical contract shape
/// to the Students module's sink so operational patterns transfer 1:1 — see
/// <c>IExternalStudentSink</c> for the full idempotency-contract narrative.
/// <para>
/// <b>Idempotency contract (REQUIRED).</b> Implementations MUST dedup on
/// <paramref name="idempotencyKey"/> — the outbox writer passes the outbox
/// row's stable <c>Id</c> so re-pushes after a SaveChanges-failure window
/// produce no duplicate external side effect.
/// </para>
/// </summary>
public interface IExternalStaffSink
{
    Task PushAsync(ExternalStaff payload, string idempotencyKey, CancellationToken cancellationToken);
}

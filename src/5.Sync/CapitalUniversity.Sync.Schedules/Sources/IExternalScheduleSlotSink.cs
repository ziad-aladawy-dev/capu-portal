using CapitalUniversity.Sync.Schedules.Domain;

namespace CapitalUniversity.Sync.Schedules.Sources;

/// <summary>
/// Push counterpart to <see cref="IExternalScheduleSlotSource"/>. See
/// <c>IExternalStudentSink</c> for the full idempotency-contract narrative.
/// <para>
/// <b>Idempotency contract (REQUIRED).</b> Implementations MUST dedup on
/// <paramref name="idempotencyKey"/> — the outbox writer passes the outbox
/// row's stable <c>Id</c>.
/// </para>
/// </summary>
public interface IExternalScheduleSlotSink
{
    Task PushAsync(ExternalScheduleSlot payload, string idempotencyKey, CancellationToken cancellationToken);
}

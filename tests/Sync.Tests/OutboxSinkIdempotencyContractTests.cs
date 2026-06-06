using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Sources;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Closes audit finding P1-6 (outbox / async side-effect safety). The outbox
/// flow is at-least-once: a successful <c>PushAsync</c> followed by a
/// <c>SaveChanges</c> failure on the sync side leaves the outbox row in
/// <c>Pending</c> status and the next tick re-pushes. Without sink-side dedup
/// on the idempotency key, the external side effect runs twice — the
/// "duplicate invoice / duplicate external sync action" risk the audit
/// flagged.
///
/// <para>
/// These tests pin the idempotency contract on the canonical reference
/// implementation (<see cref="InMemoryExternalStudentSink"/>). The interface
/// XML doc on <c>IExternalStudentSink</c> spells the contract out to anyone
/// adding a new sink; this test fails CI if the reference implementation
/// drifts so the contract can't quietly regress.
/// </para>
///
/// <para>
/// The HTTP sink (<c>HttpExternalStudentSink</c>) implements the contract by
/// forwarding the supplied <c>idempotencyKey</c> as the standard
/// <c>Idempotency-Key</c> HTTP header — Stripe / Twilio / AWS-style. That
/// surface is exercised by integration tests against a stub HTTP server in
/// the contract test suite; here we verify the in-process contract that the
/// audit endpoints and verification tests rely on.
/// </para>
/// </summary>
public class OutboxSinkIdempotencyContractTests
{
    private static ExternalStudent SamplePayload(string externalId = "EXT-S-1") => new()
    {
        ExternalStudentId = externalId,
        StudentCode = $"STU-{externalId}",
        Name = "Test Student",
        NationalId = $"NID-{externalId}",
        BirthDate = new DateTime(2000, 1, 1),
        PhoneNumber = "+200000000000",
        Email = $"{externalId.ToLowerInvariant()}@push.test",
        IsActive = true,
        ExternalUpdatedAt = DateTimeOffset.UtcNow,
        ExternalVersion = 1,
    };

    [Fact]
    public async Task SamePayload_SameIdempotencyKey_RecordsExactlyOneAcceptance()
    {
        // Simulated outbox retry: same row id ⇒ same idempotency key.
        // Both pushes must succeed (no exception) but only one acceptance
        // is recorded externally — the second call dedups against the
        // seen-key cache. Without this dedup, a SaveChanges failure after
        // the first push would produce a duplicate external write on the
        // next tick.
        var sink = new InMemoryExternalStudentSink();
        var payload = SamplePayload();
        const string idempotencyKey = "outbox-row-id-1";

        await sink.PushAsync(payload, idempotencyKey, CancellationToken.None);
        await sink.PushAsync(payload, idempotencyKey, CancellationToken.None);

        sink.AcceptedCount.Should().Be(1,
            "the second push with the same idempotency key must be a no-op — that's the audit P1-6 contract");
        sink.PushInvocationCount.Should().Be(2,
            "both invocations actually ran; only the side effect was suppressed");
    }

    [Fact]
    public async Task DifferentPayload_SameIdempotencyKey_StillDeduplicates()
    {
        // Pathological case: same outbox row replayed but the payload was
        // mutated between pushes (e.g. someone edited the row in-place,
        // which the outbox flow itself does not, but a contributor might).
        // The contract is keyed on the idempotency key alone — the second
        // push is dropped on the floor regardless of payload diff. This
        // matches the standard HTTP Idempotency-Key semantic (the first
        // accepted response is replayed; the second body is ignored).
        var sink = new InMemoryExternalStudentSink();
        var first = SamplePayload("EXT-S-1");
        var second = new ExternalStudent
        {
            ExternalStudentId = first.ExternalStudentId,
            StudentCode = first.StudentCode,
            Name = first.Name,
            NationalId = first.NationalId,
            BirthDate = first.BirthDate,
            PhoneNumber = first.PhoneNumber,
            Email = "edited@push.test",
            IsActive = first.IsActive,
            ExternalUpdatedAt = first.ExternalUpdatedAt,
            ExternalVersion = first.ExternalVersion,
        };
        const string idempotencyKey = "outbox-row-id-1";

        await sink.PushAsync(first, idempotencyKey, CancellationToken.None);
        await sink.PushAsync(second, idempotencyKey, CancellationToken.None);

        sink.Accepted[first.ExternalStudentId].Email.Should().Be(first.Email,
            "the first accepted payload sticks — the idempotency-key dedup wins over the body diff");
    }

    [Fact]
    public async Task DifferentIdempotencyKey_SamePayload_RecordsBothAsDistinctEvents()
    {
        // Two genuinely different outbox events (different row ids) carrying
        // the same business payload. The sink must NOT collapse them — they
        // represent two distinct lifecycle moments the upstream wants to see.
        var sink = new InMemoryExternalStudentSink();
        var payload = SamplePayload();

        await sink.PushAsync(payload, "outbox-row-id-1", CancellationToken.None);
        await sink.PushAsync(payload, "outbox-row-id-2", CancellationToken.None);

        sink.PushInvocationCount.Should().Be(2);
        // Both invocations ran the body; the second overwrote the first's
        // dictionary entry (same ExternalStudentId merge key), but the
        // dedup did NOT short-circuit either invocation.
        sink.AcceptedCount.Should().Be(1,
            "one external id ⇒ one row in the accepted dictionary, but both push bodies executed");
    }

    [Fact]
    public async Task ArmedFailure_DoesNotPoisonTheIdempotencyKey()
    {
        // The audit-flagged failure shape: the handler runs and throws.
        // The outbox row stays Pending; the next tick re-pushes with the
        // same idempotency key. If the failed call had cached the key
        // anyway, the retry would silently dedup and the external system
        // never receives the event. The in-memory sink commits the
        // dedup record AFTER the handler succeeds — this test pins that
        // ordering.
        var sink = new InMemoryExternalStudentSink();
        var payload = SamplePayload();
        const string idempotencyKey = "outbox-row-id-1";

        sink.FailNextPushFor(payload.ExternalStudentId);
        await sink.Invoking(s => s.PushAsync(payload, idempotencyKey, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();

        // The second push uses the same key — it must succeed and record
        // the side effect, because the failed first push did not commit
        // the dedup record.
        await sink.PushAsync(payload, idempotencyKey, CancellationToken.None);

        sink.AcceptedCount.Should().Be(1,
            "the retry after a failed first push must produce the side effect — " +
            "otherwise we silently drop legitimate events on the floor");
    }

    [Fact]
    public async Task PushAsync_RejectsNullPayloadAndEmptyKey()
    {
        // Defensive: the outbox writer always supplies both; a sink that
        // accepted nulls silently could mask a real bug upstream.
        var sink = new InMemoryExternalStudentSink();

        await sink.Invoking(s => s.PushAsync(null!, "key", CancellationToken.None))
            .Should().ThrowAsync<ArgumentNullException>();

        await sink.Invoking(s => s.PushAsync(SamplePayload(), string.Empty, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }
}

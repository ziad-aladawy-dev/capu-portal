using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Modules.Registration.Domain;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Registration.Pull;

/// <summary>
/// Sync.Registration writer — resolves each dispatch's upstream student/course
/// keys to Core ids via <see cref="ICoreWriteGateway.ResolveIdByExternalIdAsync{TEntity}"/>,
/// then upserts the <see cref="StudentRegisteredCourse"/> read-model row through
/// the gateway. Same shape as <c>ScheduleSlotWriter</c>: a dispatch whose
/// student or course is not yet present in Core is skipped (logged at Info) and
/// retried on the next tick once the upstream Student/Courses sync has landed —
/// safe because the writer is idempotent on the registration's
/// <c>ExternalId</c>.
///
/// <para>
/// Inserts are allowed: sync is the source of truth for registrations, so a
/// previously-unseen attempt is created. <c>SemesterId</c> / <c>StructureNodeId</c>
/// arrive pre-resolved on the entity (portal-native reference ids carried
/// verbatim by the mapper).
/// </para>
///
/// <para>
/// <b>Unresolved-dependency handling.</b> Rather than silently dropping a row
/// whose student/course hasn't synced yet (which the high-water-mark cursor
/// would never re-present once it advanced past the row in a mixed batch), the
/// writer records it to the failure repository as an
/// <c>UnresolvedDependency</c> dead-letter — visible in <c>sync.failures</c> and
/// replayable through the admin surface — before skipping it. In the
/// all-unresolved (zero-progress) case the pipeline still fails the run and the
/// rows replay next tick (so they may be dead-lettered again until the
/// dependency lands); those transient duplicates are bounded by the retention
/// sweeper. The alternative — inflating the persisted count to suppress the
/// replay — would corrupt the run metrics, so it is deliberately avoided.
/// </para>
/// </summary>
public sealed class RegistrationWriter : IRecordWriter<RegistrationSyncDispatch>
{
    private readonly ICoreWriteGateway _gateway;
    private readonly IFailureRepository _failures;
    private readonly ISyncRunContextAccessor _runContext;
    private readonly ILogger<RegistrationWriter> _logger;

    public RegistrationWriter(
        ICoreWriteGateway gateway,
        IFailureRepository failures,
        ISyncRunContextAccessor runContext,
        ILogger<RegistrationWriter> logger)
    {
        _gateway = gateway;
        _failures = failures;
        _runContext = runContext;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<RegistrationSyncDispatch> batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0) return 0;

        var resolved = new List<StudentRegisteredCourse>(batch.Count);
        foreach (var dispatch in batch)
        {
            var studentId = await _gateway
                .ResolveIdByExternalIdAsync<Student>(dispatch.ExternalStudentId, cancellationToken)
                .ConfigureAwait(false);
            if (studentId is null)
            {
                _logger.LogInformation(
                    "Sync.Registration: dead-lettering registration ExternalId={ExternalRegistrationId} — upstream student key {ExternalStudentId} has no matching Core Student yet.",
                    dispatch.Entity.ExternallySourced.ExternalId, dispatch.ExternalStudentId);
                await DeadLetterAsync(
                    dispatch.Entity.ExternallySourced.ExternalId,
                    $"Unresolved student dependency: upstream student '{dispatch.ExternalStudentId}' is not yet in Core.",
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            var courseId = await _gateway
                .ResolveIdByExternalIdAsync<Course>(dispatch.ExternalCourseId, cancellationToken)
                .ConfigureAwait(false);
            if (courseId is null)
            {
                _logger.LogInformation(
                    "Sync.Registration: dead-lettering registration ExternalId={ExternalRegistrationId} — upstream course key {ExternalCourseId} has no matching Core Course yet.",
                    dispatch.Entity.ExternallySourced.ExternalId, dispatch.ExternalCourseId);
                await DeadLetterAsync(
                    dispatch.Entity.ExternallySourced.ExternalId,
                    $"Unresolved course dependency: upstream course '{dispatch.ExternalCourseId}' is not yet in Core.",
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            dispatch.Entity.StudentId = studentId.Value;
            dispatch.Entity.CourseId = courseId.Value;
            resolved.Add(dispatch.Entity);
        }

        if (resolved.Count == 0) return 0;

        var result = await _gateway.UpsertAsync<StudentRegisteredCourse>(
            resolved,
            applyUpdate: (existing, incoming) =>
            {
                // Sync owns every field of this read-model row. Student/Course
                // rebind is permitted — upstream may correct a misassignment,
                // same rationale as Invoice.StudentId / ScheduleSlot.CourseOfferingId.
                existing.StudentId = incoming.StudentId;
                existing.CourseId = incoming.CourseId;
                existing.SemesterId = incoming.SemesterId;
                existing.StructureNodeId = incoming.StructureNodeId;
                existing.AttemptNumber = incoming.AttemptNumber;
                existing.RegistrationStatus = incoming.RegistrationStatus;
                existing.RegisteredAt = incoming.RegisteredAt;
                existing.CompletedAt = incoming.CompletedAt;
            },
            new CoreUpsertOptions { AllowInsert = true, RespectExternalUpdatedAt = true },
            cancellationToken).ConfigureAwait(false);

        if (result.SkippedNotNewer > 0)
        {
            _logger.LogDebug(
                "Sync.Registration: {SkippedNotNewer} stale upstream replays skipped (external-wins guard).",
                result.SkippedNotNewer);
        }

        return result.Persisted;
    }

    /// <summary>
    /// Records an unresolvable registration to <c>sync.failures</c> for visibility
    /// and admin replay. The run's <see cref="SyncContext"/> supplies the
    /// correlation/attempt; outside a run (e.g. a direct unit test) it falls back
    /// to <see cref="Guid.Empty"/> / attempt 1. The dead-letter write is
    /// best-effort — a failure-store hiccup must not abort the batch of good rows.
    /// </summary>
    private async Task DeadLetterAsync(string? externalRegistrationId, string reason, CancellationToken cancellationToken)
    {
        var context = _runContext.Current;
        try
        {
            await _failures.RecordAsync(new SyncFailureRecord
            {
                CorrelationId = context?.CorrelationId ?? Guid.Empty,
                Attempt = context?.Attempt ?? 1,
                ErrorType = "UnresolvedDependency",
                ErrorMessage = $"Registration '{externalRegistrationId}' dead-lettered. {reason}",
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Sync.Registration: failed to dead-letter unresolved registration ExternalId={ExternalRegistrationId}. The row will be retried on the next tick.",
                externalRegistrationId);
        }
    }
}

using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Modules.Registration.Domain;
using CapitalUniversity.Sync.Abstractions.Contracts;
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
/// </summary>
public sealed class RegistrationWriter : IRecordWriter<RegistrationSyncDispatch>
{
    private readonly ICoreWriteGateway _gateway;
    private readonly ILogger<RegistrationWriter> _logger;

    public RegistrationWriter(ICoreWriteGateway gateway, ILogger<RegistrationWriter> logger)
    {
        _gateway = gateway;
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
                    "Sync.Registration: skipping registration ExternalId={ExternalRegistrationId} — upstream student key {ExternalStudentId} has no matching Core Student yet.",
                    dispatch.Entity.ExternallySourced.ExternalId, dispatch.ExternalStudentId);
                continue;
            }

            var courseId = await _gateway
                .ResolveIdByExternalIdAsync<Course>(dispatch.ExternalCourseId, cancellationToken)
                .ConfigureAwait(false);
            if (courseId is null)
            {
                _logger.LogInformation(
                    "Sync.Registration: skipping registration ExternalId={ExternalRegistrationId} — upstream course key {ExternalCourseId} has no matching Core Course yet.",
                    dispatch.Entity.ExternallySourced.ExternalId, dispatch.ExternalCourseId);
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
}

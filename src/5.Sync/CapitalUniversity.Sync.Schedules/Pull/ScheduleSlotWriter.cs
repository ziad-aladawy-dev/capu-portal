using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Modules.CourseOffering.Domain;
using CapitalUniversity.Modules.Schedule.Domain;
using CapitalUniversity.Sync.Abstractions.Contracts;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Schedules.Pull;

/// <summary>
/// Sync.Schedules writer — resolves each dispatch's
/// <c>ExternalCourseOfferingId</c> to a Core <c>CourseOffering.Id</c> via
/// <see cref="ICoreWriteGateway.ResolveIdByExternalIdAsync"/>, sets it on the
/// <see cref="ScheduleSlot"/>, then upserts through the gateway.
/// </summary>
public sealed class ScheduleSlotWriter : IRecordWriter<ScheduleSlotSyncDispatch>
{
    private readonly ICoreWriteGateway _gateway;
    private readonly ILogger<ScheduleSlotWriter> _logger;

    public ScheduleSlotWriter(ICoreWriteGateway gateway, ILogger<ScheduleSlotWriter> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<ScheduleSlotSyncDispatch> batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0) return 0;

        var resolved = new List<ScheduleSlot>(batch.Count);
        foreach (var dispatch in batch)
        {
            var offeringId = await _gateway
                .ResolveIdByExternalIdAsync<CourseOffering>(dispatch.ExternalCourseOfferingId, cancellationToken)
                .ConfigureAwait(false);

            if (offeringId is null)
            {
                _logger.LogInformation(
                    "Sync.Schedules: skipping ScheduleSlot ExternalId={ExternalSlotId} — upstream offering key {ExternalOfferingId} has no matching Core CourseOffering yet.",
                    dispatch.Entity.ExternallySourced.ExternalId, dispatch.ExternalCourseOfferingId);
                continue;
            }

            dispatch.Entity.CourseOfferingId = offeringId.Value;
            resolved.Add(dispatch.Entity);
        }

        if (resolved.Count == 0) return 0;

        var result = await _gateway.UpsertAsync<ScheduleSlot>(
            resolved,
            applyUpdate: (existing, incoming) =>
            {
                // CourseOfferingId rebind: same rationale as Invoice.StudentId
                // — upstream may correct a misassignment.
                existing.CourseOfferingId = incoming.CourseOfferingId;
                existing.DayOfWeek = incoming.DayOfWeek;
                // SetTimeRange enforces End > Start on existing too. EnsureMutable
                // throws if the existing slot is closed — sync will surface that
                // as a per-row failure (intended: closed slots stay immutable).
                existing.SetTimeRange(incoming.StartTime, incoming.EndTime);
                existing.Kind = incoming.Kind;
                existing.Location = incoming.Location;
                existing.Notes = incoming.Notes;
            },
            new CoreUpsertOptions { AllowInsert = true, RespectExternalUpdatedAt = true },
            cancellationToken).ConfigureAwait(false);

        return result.Persisted;
    }
}

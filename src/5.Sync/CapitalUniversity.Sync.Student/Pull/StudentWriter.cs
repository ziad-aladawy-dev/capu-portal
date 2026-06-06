using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Sync.Abstractions.Contracts;
using Microsoft.Extensions.Logging;

// See StudentMapper.cs — Sync.Student namespace collides with Core's Student type.
using CoreStudent = CapitalUniversity.Core.Domain.Identity.Student;

namespace CapitalUniversity.Sync.Student.Pull;

/// <summary>
/// Sync.Student's writer is a thin adapter over <see cref="ICoreWriteGateway"/>.
/// It hands the gateway a batch of Core Student entities and a merge delegate
/// that copies only the columns sync owns — leaving <c>PasswordHash</c>,
/// <c>StructureNodeId</c>, <c>PasswordExpiry</c>, <c>SessionVersion</c>
/// untouched on existing rows.
/// <para>
/// Sync.Student is configured with <see cref="CoreUpsertOptions.AllowInsert"/>
/// = <c>false</c> because Core's Student requires a <c>StructureNodeId</c>
/// that sync isn't allowed to source. Upstream rows whose <c>ExternalId</c>
/// isn't already in Core get skipped and counted under
/// <see cref="CoreUpsertResult.SkippedNotFound"/>.
/// </para>
/// </summary>
public sealed class StudentWriter : IRecordWriter<CoreStudent>
{
    private readonly ICoreWriteGateway _gateway;
    private readonly ILogger<StudentWriter> _logger;

    public StudentWriter(ICoreWriteGateway gateway, ILogger<StudentWriter> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<CoreStudent> batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0) return 0;

        var result = await _gateway.UpsertAsync<CoreStudent>(
            batch,
            applyUpdate: ApplyExternalToExisting,
            new CoreUpsertOptions
            {
                // Student requires StructureNodeId, which is out of sync scope —
                // never insert from sync; only update existing Core rows.
                AllowInsert = false,
                RespectExternalUpdatedAt = true
            },
            cancellationToken).ConfigureAwait(false);

        if (result.SkippedNotFound > 0)
        {
            _logger.LogInformation(
                "Sync.Student: {SkippedNotFound} upstream rows had no matching Core student (update-only mode). Total persisted={Persisted}.",
                result.SkippedNotFound, result.Persisted);
        }

        return result.Persisted;
    }

    /// <summary>
    /// Field-by-field merge from the upstream snapshot onto the existing Core
    /// row. Columns sync DOES NOT touch (auth + structure FK + lifecycle):
    /// <c>PasswordHash</c>, <c>PasswordExpiry</c>, <c>SessionVersion</c>,
    /// <c>StructureNodeId</c>, <c>StructureNode</c>. The gateway stamps
    /// ExternallySourcedEntity columns separately.
    /// </summary>
    private static void ApplyExternalToExisting(CoreStudent existing, CoreStudent incoming)
    {
        existing.StudentCode = incoming.StudentCode;
        existing.Name = incoming.Name;
        existing.NationalId = incoming.NationalId;
        existing.BirthDate = incoming.BirthDate;
        existing.PhoneNumber = incoming.PhoneNumber;
        existing.Email = incoming.Email;
        existing.IsActive = incoming.IsActive;
    }
}

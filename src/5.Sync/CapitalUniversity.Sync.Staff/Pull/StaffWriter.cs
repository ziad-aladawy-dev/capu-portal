using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Sync.Abstractions.Contracts;
using Microsoft.Extensions.Logging;

using CoreStaff = CapitalUniversity.Core.Domain.Identity.Staff;

namespace CapitalUniversity.Sync.Staff.Pull;

/// <summary>
/// Sync.Staff writer — thin adapter over <see cref="ICoreWriteGateway"/>.
/// Update-only (StructureNode is out of sync scope, same as Sync.Student).
/// </summary>
public sealed class StaffWriter : IRecordWriter<CoreStaff>
{
    private readonly ICoreWriteGateway _gateway;
    private readonly ILogger<StaffWriter> _logger;

    public StaffWriter(ICoreWriteGateway gateway, ILogger<StaffWriter> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<CoreStaff> batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0) return 0;

        var result = await _gateway.UpsertAsync<CoreStaff>(
            batch,
            applyUpdate: ApplyExternalToExisting,
            new CoreUpsertOptions { AllowInsert = false, RespectExternalUpdatedAt = true },
            cancellationToken).ConfigureAwait(false);

        if (result.SkippedNotFound > 0)
        {
            _logger.LogInformation(
                "Sync.Staff: {SkippedNotFound} upstream rows had no matching Core staff (update-only mode). Total persisted={Persisted}.",
                result.SkippedNotFound, result.Persisted);
        }

        return result.Persisted;
    }

    /// <summary>
    /// Copies only the columns sync owns onto the existing Core row. Untouched:
    /// <c>PasswordHash</c>, <c>PasswordExpiry</c>, <c>SessionVersion</c>,
    /// <c>StructureNodeId</c>, <c>StructureNode</c>.
    /// </summary>
    private static void ApplyExternalToExisting(CoreStaff existing, CoreStaff incoming)
    {
        existing.EmployeeCode = incoming.EmployeeCode;
        existing.Name = incoming.Name;
        existing.JobTitle = incoming.JobTitle;
        existing.NationalId = incoming.NationalId;
        existing.BirthDate = incoming.BirthDate;
        existing.PhoneNumber = incoming.PhoneNumber;
        existing.Email = incoming.Email;
        existing.Role = incoming.Role;
        existing.IsActive = incoming.IsActive;
    }
}

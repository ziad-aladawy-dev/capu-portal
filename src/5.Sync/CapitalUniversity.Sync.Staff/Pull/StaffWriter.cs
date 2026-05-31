using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Staff.Domain;
using CapitalUniversity.Sync.Staff.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Staff.Pull;

/// <summary>
/// EF upsert keyed by <see cref="StaffEntity.ExternalStaffId"/>. Mirrors the
/// Students writer's idempotency + race-safety pattern (one retry on unique-index
/// violation; external-wins on update).
/// </summary>
public sealed class StaffWriter : IRecordWriter<StaffEntity>
{
    private const int MaxAttempts = 2;

    private readonly StaffSyncDbContext _db;
    private readonly ILogger<StaffWriter> _logger;

    public StaffWriter(StaffSyncDbContext db, ILogger<StaffWriter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<StaffEntity> batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0) return 0;

        // See StudentWriter for rationale — bound tracker memory to one batch.
        _db.ChangeTracker.Clear();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await UpsertOnceAsync(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxAttempts)
            {
                _db.ChangeTracker.Clear();
                _logger.LogInformation(
                    "StaffWriter unique-constraint race detected; converging via retry. BatchSize={Size} Attempt={Attempt}.",
                    batch.Count, attempt);
            }
        }

        return 0;
    }

    private async Task<int> UpsertOnceAsync(IReadOnlyList<StaffEntity> batch, CancellationToken cancellationToken)
    {
        var externalIds = batch.Select(x => x.ExternalStaffId).ToArray();

        var existing = await _db.Staff
            .Where(s => externalIds.Contains(s.ExternalStaffId))
            .ToDictionaryAsync(s => s.ExternalStaffId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var incoming in batch)
        {
            if (existing.TryGetValue(incoming.ExternalStaffId, out var current))
            {
                current.FirstName = incoming.FirstName;
                current.LastName = incoming.LastName;
                current.Email = incoming.Email;
                current.Department = incoming.Department;
                current.ExternalUpdatedAt = incoming.ExternalUpdatedAt;
                current.ExternalVersion = incoming.ExternalVersion;
                current.LastSyncedAt = incoming.LastSyncedAt;
                current.OriginSystem = incoming.OriginSystem;
            }
            else
            {
                _db.Staff.Add(incoming);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return batch.Count;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sql && (sql.Number == 2627 || sql.Number == 2601);
    }
}
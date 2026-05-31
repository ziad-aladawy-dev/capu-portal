using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Sync.Student.Pull;

/// <summary>
/// Idempotent EF upsert keyed by <see cref="StudentEntity.ExternalStudentId"/>.
///
/// <para>
/// <b>Replay-safe.</b> Re-running this writer with the same batch produces the same
/// final state: existing rows are updated in place; new rows are inserted. The
/// pipeline's idempotency handler dedups within a single run; the writer's external-key
/// upsert dedups across runs.
/// </para>
///
/// <para>
/// <b>Race-safe.</b> The classic read-then-write race (two workers both observe
/// "row does not exist" and both attempt an insert) is caught at <c>SaveChangesAsync</c>
/// as a unique-constraint <see cref="DbUpdateException"/> on
/// <c>IX_students_ExternalStudentId</c>. The writer clears its change tracker,
/// re-reads existing rows, and retries the upsert once. Persistent conflicts surface
/// as exceptions so Hangfire's retry policy and the audit trail can engage.
/// </para>
///
/// <para>
/// <b>External-wins.</b> When an existing row is found, the incoming external values
/// overwrite the internal values per <c>Sync_Platform_Model.md</c>'s conflict-resolution
/// rule.
/// </para>
/// </summary>
public sealed class StudentWriter : IRecordWriter<StudentEntity>
{
    private const int MaxAttempts = 2;

    private readonly StudentSyncDbContext _db;
    private readonly ILogger<StudentWriter> _logger;

    public StudentWriter(StudentSyncDbContext db, ILogger<StudentWriter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<StudentEntity> batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0)
        {
            return 0;
        }

        // DbContext is scoped to the whole pipeline run, so entities tracked by
        // a prior batch would otherwise stay attached and grow the change tracker
        // across batches. Clear before each batch so memory is bounded by batch
        // size, not by total records seen.
        _db.ChangeTracker.Clear();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await UpsertOnceAsync(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxAttempts)
            {
                // Another worker inserted between our SELECT and INSERT. Drop our pending
                // changes, re-read existing rows on the next pass, and let the upsert
                // path apply external-wins on the now-present rows.
                _db.ChangeTracker.Clear();
                _logger.LogInformation(
                    "StudentWriter unique-constraint race detected; converging via retry. BatchSize={Size} Attempt={Attempt}.",
                    batch.Count, attempt);
            }
        }

        // Unreachable when MaxAttempts >= 1; satisfies the compiler.
        return 0;
    }

    private async Task<int> UpsertOnceAsync(
        IReadOnlyList<StudentEntity> batch,
        CancellationToken cancellationToken)
    {
        var externalIds = batch.Select(x => x.ExternalStudentId).ToArray();

        var existing = await _db.Students
            .Where(s => externalIds.Contains(s.ExternalStudentId))
            .ToDictionaryAsync(s => s.ExternalStudentId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var incoming in batch)
        {
            if (existing.TryGetValue(incoming.ExternalStudentId, out var current))
            {
                // External wins.
                current.FirstName = incoming.FirstName;
                current.LastName = incoming.LastName;
                current.Email = incoming.Email;
                current.ExternalUpdatedAt = incoming.ExternalUpdatedAt;
                current.ExternalVersion = incoming.ExternalVersion;
                current.LastSyncedAt = incoming.LastSyncedAt;
                current.OriginSystem = incoming.OriginSystem;
            }
            else
            {
                _db.Students.Add(incoming);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return batch.Count;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // SQL Server: 2627 = unique constraint, 2601 = unique index.
        return ex.InnerException is SqlException sql &&
               (sql.Number == 2627 || sql.Number == 2601);
    }
}
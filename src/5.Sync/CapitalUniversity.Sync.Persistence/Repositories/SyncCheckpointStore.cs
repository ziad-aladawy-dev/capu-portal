using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Persistence.Context;
using CapitalUniversity.Sync.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Sync.Persistence.Repositories;

public sealed class SyncCheckpointStore : ISyncCheckpointStore
{
    private readonly SyncDbContext _db;

    public SyncCheckpointStore(SyncDbContext db)
    {
        _db = db;
    }

    public async Task<SyncCheckpoint?> GetAsync(string moduleName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("Module name required.", nameof(moduleName));
        }

        var entity = await _db.Checkpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ModuleName == moduleName, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : new SyncCheckpoint
        {
            ModuleName = entity.ModuleName,
            LastSyncedAt = entity.LastSyncedAt,
            Cursor = entity.Cursor
        };
    }

    public async Task SaveAsync(
        string moduleName,
        SyncCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var entity = await _db.Checkpoints
            .FirstOrDefaultAsync(x => x.ModuleName == moduleName, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            _db.Checkpoints.Add(new SyncCheckpointEntity
            {
                ModuleName = moduleName,
                LastSyncedAt = checkpoint.LastSyncedAt,
                Cursor = checkpoint.Cursor,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            entity.LastSyncedAt = checkpoint.LastSyncedAt;
            entity.Cursor = checkpoint.Cursor;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // entity.LastRowVersion / LastExternalVersion columns remain in the DB
        // but are no longer round-tripped through the abstraction. Future schema
        // cleanup can drop them via a migration when convenient.

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Core.Infrastructure.Sync;

/// <summary>
/// Sole concrete implementation of <see cref="ICoreWriteGateway"/>. Sync modules
/// resolve this via DI through the abstraction; they never touch
/// <see cref="CoreDbContext"/> directly.
///
/// <para>
/// The gateway is generic over <see cref="BaseEntity"/> + <see cref="IExternallySourced"/>
/// so the same implementation handles every synced type — Student, Staff,
/// Course (Core domain) and Invoice, ScheduleSlot, CourseOffering
/// (module-owned). EF tracks the module-owned types because each module
/// contributes its configuration assembly through
/// <see cref="CoreDbContext.ModuleConfigurationAssemblies"/>. The
/// <see cref="ExternallySourcedData"/> block is reached through composition
/// (<see cref="IExternallySourced.ExternallySourced"/>) and persisted via
/// EF's <c>OwnsOne</c> mapping configured per entity — same physical columns
/// as the prior inheritance-era schema.
/// </para>
/// </summary>
public sealed class CoreWriteGateway : ICoreWriteGateway
{
    private readonly CoreDbContext _db;
    private readonly ILogger<CoreWriteGateway> _logger;

    public CoreWriteGateway(CoreDbContext db, ILogger<CoreWriteGateway> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CoreUpsertResult> UpsertAsync<TEntity>(
        IReadOnlyList<TEntity> incoming,
        Action<TEntity, TEntity> applyUpdate,
        CoreUpsertOptions options,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity, IExternallySourced
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(applyUpdate);
        ArgumentNullException.ThrowIfNull(options);

        if (incoming.Count == 0)
        {
            return new CoreUpsertResult();
        }

        // Drop rows that arrived without an ExternalId — the gateway needs it
        // as the merge key. We count + log them so a misconfigured upstream
        // can't silently no-op.
        var withKey = new List<TEntity>(incoming.Count);
        var skippedNoExternalId = 0;
        foreach (var e in incoming)
        {
            if (string.IsNullOrWhiteSpace(e.ExternallySourced.ExternalId))
            {
                skippedNoExternalId++;
                _logger.LogWarning(
                    "CoreWriteGateway: {EntityType} arrived without ExternalId — skipped.",
                    typeof(TEntity).Name);
                continue;
            }
            withKey.Add(e);
        }

        if (withKey.Count == 0)
        {
            return new CoreUpsertResult { SkippedNoExternalId = skippedNoExternalId };
        }

        // C2 — the read-existing → Add-missing → SaveChanges sequence is a
        // check-then-act: a concurrent writer can insert the same ExternalId in
        // the window between our read and our save, surfacing as a unique
        // violation that would otherwise crash the whole sync batch. Retry once;
        // the second pass re-reads, sees the now-present row as an update, and
        // commits cleanly. One retry suffices — a sustained stream of colliding
        // inserts for the same key is not a real sync scenario.
        const int maxAttempts = 2;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await UpsertTrackedBatchAsync().ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts && IsUniqueViolation(ex))
            {
                _logger.LogWarning(ex,
                    "CoreWriteGateway: concurrent insert collided on {EntityType}; clearing tracker and retrying once.",
                    typeof(TEntity).Name);
                // Detach the half-applied batch so the retry re-reads a clean
                // slate — the conflicting row now resolves as an update.
                _db.ChangeTracker.Clear();
            }
        }

        async Task<CoreUpsertResult> UpsertTrackedBatchAsync()
        {
            // Single round-trip to fetch the existing rows for this batch. The
            // ExternalId column lives on the owned navigation; EF translates the
            // ExternallySourced.ExternalId lambda into a query against the
            // physical column on the parent table.
            var externalIds = withKey
                .Select(e => e.ExternallySourced.ExternalId!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var existing = await _db.Set<TEntity>()
                .Where(e => externalIds.Contains(e.ExternallySourced.ExternalId))
                .ToDictionaryAsync(e => e.ExternallySourced.ExternalId!, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);

            var persisted = 0;
            var skippedNotFound = 0;
            var skippedNotNewer = 0;
            var now = DateTime.UtcNow;

            foreach (var entity in withKey)
            {
                if (existing.TryGetValue(entity.ExternallySourced.ExternalId!, out var current))
                {
                    // External-wins guard: skip on stale upstream replay.
                    if (options.RespectExternalUpdatedAt
                        && current.ExternallySourced.ExternalUpdatedAt is { } currentStamp
                        && entity.ExternallySourced.ExternalUpdatedAt is { } incomingStamp
                        && incomingStamp <= currentStamp)
                    {
                        skippedNotNewer++;
                        continue;
                    }

                    // Caller chooses which scalar columns the sync layer owns —
                    // see the docs on Action<TEntity, TEntity>. The gateway is
                    // responsible only for the sync-metadata stamping below.
                    applyUpdate(current, entity);

                    current.ExternallySourced.ExternalUpdatedAt = entity.ExternallySourced.ExternalUpdatedAt;
                    current.ExternallySourced.ExternalVersion = entity.ExternallySourced.ExternalVersion;
                    current.ExternallySourced.LastSyncedAt = now;
                    current.ExternallySourced.OriginSystem = "external";

                    persisted++;
                }
                else if (options.AllowInsert)
                {
                    // Insert path — sync's mapper already produced a complete
                    // entity. We stamp the sync-metadata and add as-is.
                    entity.ExternallySourced.LastSyncedAt = now;
                    entity.ExternallySourced.OriginSystem = "external";
                    _db.Set<TEntity>().Add(entity);
                    persisted++;
                }
                else
                {
                    skippedNotFound++;
                    _logger.LogInformation(
                        "CoreWriteGateway: {EntityType} ExternalId={ExternalId} not pre-provisioned in Core, update-only mode skipped insert.",
                        typeof(TEntity).Name, entity.ExternallySourced.ExternalId);
                }
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new CoreUpsertResult
            {
                Persisted = persisted,
                SkippedNotFound = skippedNotFound,
                SkippedNotNewer = skippedNotNewer,
                SkippedNoExternalId = skippedNoExternalId
            };
        }
    }

    // SQL Server unique-violation error numbers (2601 unique index, 2627 unique
    // constraint). Read off the provider's SqlException via reflection so this
    // assembly keeps no hard reference to Microsoft.Data.SqlClient — same
    // approach GlobalExceptionHandler uses on the API side.
    private const int SqlUniqueIndexViolation = 2601;
    private const int SqlUniqueConstraintViolation = 2627;

    private static bool IsUniqueViolation(DbUpdateException dbEx)
    {
        var inner = dbEx.InnerException;
        if (inner is null) return false;
        var numberProp = inner.GetType().GetProperty("Number");
        if (numberProp is null) return false;
        var number = (int?)numberProp.GetValue(inner);
        return number == SqlUniqueIndexViolation || number == SqlUniqueConstraintViolation;
    }

    public async Task<Guid?> ResolveIdByExternalIdAsync<TEntity>(
        string externalId,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity, IExternallySourced
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        // AsNoTracking — we're only resolving a Guid, not staging the row for
        // mutation. Avoids polluting the change tracker when the gateway is
        // called multiple times within one DbContext lifetime.
        return await _db.Set<TEntity>()
            .AsNoTracking()
            .Where(e => e.ExternallySourced.ExternalId == externalId)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

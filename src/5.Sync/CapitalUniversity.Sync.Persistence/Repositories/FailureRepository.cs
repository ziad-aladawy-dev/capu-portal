using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Persistence.Context;
using CapitalUniversity.Sync.Persistence.Entities;

namespace CapitalUniversity.Sync.Persistence.Repositories;

public sealed class FailureRepository : IFailureRepository
{
    private readonly SyncDbContext _db;

    public FailureRepository(SyncDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(SyncFailureRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        _db.Failures.Add(new SyncFailureEntity
        {
            CorrelationId = record.CorrelationId,
            HangfireJobId = record.HangfireJobId,
            Attempt = record.Attempt,
            OccurredAt = record.OccurredAt,
            ErrorType = TextHelpers.Truncate(record.ErrorType, 256) ?? string.Empty,
            ErrorMessage = TextHelpers.Truncate(record.ErrorMessage, 4000) ?? string.Empty,
            StackTrace = record.StackTrace
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
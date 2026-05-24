using System;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authentication;

public class SessionVersionService : ISessionVersionService
{
    private readonly CoreDbContext _dbContext;

    public SessionVersionService(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int?> GetCurrentVersionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Try Staff first, then Student. Either lookup hits a unique-PK index, so two
        // round-trips at worst — and both queries are projected to the integer column
        // only, no full-row read.
        var staffVersion = await _dbContext.Staffs
            .AsNoTracking()
            .Where(s => s.Id == userId)
            .Select(s => (int?)s.SessionVersion)
            .FirstOrDefaultAsync(cancellationToken);

        if (staffVersion.HasValue) return staffVersion;

        return await _dbContext.Students
            .AsNoTracking()
            .Where(s => s.Id == userId)
            .Select(s => (int?)s.SessionVersion)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int?> IncrementVersionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // H11 — atomic increment. The previous read-modify-write let two
        // concurrent logout / password-change writers both observe v=5 and
        // both persist v=6, leaving the version effectively un-bumped and
        // any token issued at v=5 still valid.
        //
        // On a relational provider we push the +1 into a single SQL UPDATE
        // (server-side row lock), which closes the lost-update window. The
        // InMemory provider used by tests has no SQL backend, so we keep the
        // ORM path as a fallback there — single-threaded test runs cannot
        // race, so the absence of atomicity is harmless in that context.
        if (_dbContext.Database.IsRelational())
        {
            var staffAffected = await _dbContext.Staffs
                .Where(s => s.Id == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(s => s.SessionVersion, s => s.SessionVersion + 1),
                    cancellationToken);

            if (staffAffected > 0)
            {
                return await _dbContext.Staffs
                    .AsNoTracking()
                    .Where(s => s.Id == userId)
                    .Select(s => (int?)s.SessionVersion)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var studentAffected = await _dbContext.Students
                .Where(s => s.Id == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(s => s.SessionVersion, s => s.SessionVersion + 1),
                    cancellationToken);

            if (studentAffected > 0)
            {
                return await _dbContext.Students
                    .AsNoTracking()
                    .Where(s => s.Id == userId)
                    .Select(s => (int?)s.SessionVersion)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return null;
        }

        // Non-relational fallback (InMemory tests). Same read-modify-write as
        // before; safe under the single-writer assumption that holds for tests.
        var staff = await _dbContext.Staffs.FirstOrDefaultAsync(s => s.Id == userId, cancellationToken);
        if (staff != null)
        {
            staff.SessionVersion += 1;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return staff.SessionVersion;
        }

        var student = await _dbContext.Students.FirstOrDefaultAsync(s => s.Id == userId, cancellationToken);
        if (student != null)
        {
            student.SessionVersion += 1;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return student.SessionVersion;
        }

        return null;
    }

    public Task InvalidateCacheAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Non-cached implementation: nothing to drop. The decorator overrides
        // this with the real Redis/memory eviction.
        return Task.CompletedTask;
    }
}

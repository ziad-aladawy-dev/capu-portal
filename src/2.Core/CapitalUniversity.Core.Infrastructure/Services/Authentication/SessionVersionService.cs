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

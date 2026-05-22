using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization;

public class PermissionCacheInvalidator : IPermissionCacheInvalidator
{
    /// <summary>
    /// Cache key for the global "epoch" stamp. <see cref="PermissionManagementService.GetPermissionLookupAsync"/>
    /// folds this into every per-user lookup key (via the version stamp seed), so
    /// rotating it orphans every cached entry across all users in one write.
    /// </summary>
    public const string GlobalEpochKey = "perm_lookup_epoch";

    public static string UserVersionKey(Guid userId) => $"perm_lookup_v_{userId}";

    private readonly ICacheService _cache;
    private readonly CoreDbContext _dbContext;
    private readonly PermissionCacheOptions _options;

    public PermissionCacheInvalidator(
        ICacheService cache,
        CoreDbContext dbContext,
        IOptions<PermissionCacheOptions>? options = null)
    {
        _cache = cache;
        _dbContext = dbContext;
        _options = options?.Value ?? new PermissionCacheOptions();
    }

    public Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _cache.SetAsync(
            UserVersionKey(userId),
            Guid.NewGuid().ToString("N"),
            TimeSpan.FromHours(_options.VersionTtlHours),
            cancellationToken);

    public async Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        // Walk current assignees and rotate each — bounded by current staff count,
        // not by historical cache entries. Idempotent: each new version stamp is
        // a fresh Guid so concurrent rotations don't fight over a single value.
        var staffIds = await _dbContext.StaffRoles
            .AsNoTracking()
            .Where(sr => sr.RoleId == roleId)
            .Select(sr => sr.StaffId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var staffId in staffIds)
        {
            await InvalidateUserAsync(staffId, cancellationToken);
        }
    }

    public Task InvalidateAllAsync(CancellationToken cancellationToken = default) =>
        _cache.SetAsync(
            GlobalEpochKey,
            Guid.NewGuid().ToString("N"),
            TimeSpan.FromHours(_options.VersionTtlHours),
            cancellationToken);
}

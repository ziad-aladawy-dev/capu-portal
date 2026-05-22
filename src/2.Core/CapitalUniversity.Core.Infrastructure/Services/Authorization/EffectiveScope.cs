using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization;

/// <summary>
/// Row-level access decisions for the three in-scope services. Resolves the
/// target row's StructureNode.Path on demand and compares it against the
/// caller's <see cref="IUserScope"/>. Caches lookups per-request via the
/// scoped DbContext's first-level cache.
/// </summary>
public sealed class EffectiveScope : IEffectiveScope
{
    private readonly IUserScope _userScope;
    private readonly CoreDbContext _dbContext;
    private readonly IExecutionContext _executionContext;

    // Per-request memoisation. The DbContext is scoped, so these caches live
    // exactly as long as the request that owns them.
    private readonly Dictionary<Guid, string?> _studentPaths = new();
    private readonly Dictionary<Guid, string?> _nodePaths = new();

    public EffectiveScope(IUserScope userScope, CoreDbContext dbContext, IExecutionContext executionContext)
    {
        _userScope = userScope;
        _dbContext = dbContext;
        _executionContext = executionContext;
    }

    public async Task<bool> CanAccessStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        // P1.1 — Runtime Hardening §3: background / system tasks (outbox, 
        // dispatcher) bypass per-user scope checks. If the system flag is set,
        // we permit the operation without requiring an authenticated user.
        if (_executionContext.IsSystem) return true;

        await _userScope.EnsureLoadedAsync(cancellationToken);

        // Unauthenticated → no rows are visible. The middleware should have
        // already short-circuited this, but defence in depth.
        if (_userScope.UserId == Guid.Empty) return false;

        if (_userScope.IsStudent)
        {
            // Students see only their own owner rows.
            return studentId == _userScope.UserId;
        }

        if (_userScope.HasGlobalScope) return true;

        var studentPath = await ResolveStudentPathAsync(studentId, cancellationToken);
        if (studentPath is null) return false;

        return PathCoveredByGrants(studentPath);
    }

    public async Task<bool> CanAccessStructureNodeAsync(Guid structureNodeId, CancellationToken cancellationToken = default)
    {
        if (_executionContext.IsSystem) return true;

        await _userScope.EnsureLoadedAsync(cancellationToken);
        if (_userScope.UserId == Guid.Empty) return false;

        var nodePath = await ResolveNodePathAsync(structureNodeId, cancellationToken);
        if (nodePath is null) return false;

        if (_userScope.IsStudent)
        {
            // Students see nodes that are on their ancestor path: the node is
            // the student's node itself OR an ancestor (so they see their plan
            // even if it's owned by a higher node).
            var ownPath = _userScope.OwnStructureNodePath;
            if (string.IsNullOrEmpty(ownPath)) return false;
            return ownPath.StartsWith(nodePath, StringComparison.OrdinalIgnoreCase);
        }

        if (_userScope.HasGlobalScope) return true;

        return PathCoveredByGrants(nodePath);
    }

    private bool PathCoveredByGrants(string targetPath) =>
        _userScope.AuthorizedNodePaths
            .Any(grant => targetPath.StartsWith(grant, StringComparison.OrdinalIgnoreCase));

    private async Task<string?> ResolveStudentPathAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (_studentPaths.TryGetValue(studentId, out var cached)) return cached;

        // Join Student → StructureNode in one round-trip.
        var path = await _dbContext.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => _dbContext.StructureNodes.Where(n => n.Id == s.StructureNodeId).Select(n => n.Path).FirstOrDefault())
            .FirstOrDefaultAsync(cancellationToken);

        _studentPaths[studentId] = path;
        return path;
    }

    private async Task<string?> ResolveNodePathAsync(Guid nodeId, CancellationToken cancellationToken)
    {
        if (_nodePaths.TryGetValue(nodeId, out var cached)) return cached;

        var path = await _dbContext.StructureNodes
            .AsNoTracking()
            .Where(n => n.Id == nodeId)
            .Select(n => n.Path)
            .FirstOrDefaultAsync(cancellationToken);

        _nodePaths[nodeId] = path;
        return path;
    }
}

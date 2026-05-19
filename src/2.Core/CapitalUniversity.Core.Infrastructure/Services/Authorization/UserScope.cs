using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization;

/// <summary>
/// Scoped per-request lazy resolver. The first call to any property forces a
/// one-shot load: Student/Staff row + the active StaffRoleAssignments' path
/// grants. Subsequent calls reuse the cached state for the request.
/// </summary>
public sealed class UserScope : IUserScope
{
    private readonly ICurrentUser _currentUser;
    private readonly CoreDbContext _dbContext;
    private bool _loaded;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Guid _userId;
    private bool _isStudent;
    private Guid? _ownNodeId;
    private string? _ownPath;
    private HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private bool _global;

    public UserScope(ICurrentUser currentUser, CoreDbContext dbContext)
    {
        _currentUser = currentUser;
        _dbContext = dbContext;
    }

    public Guid UserId
    {
        get { EnsureLoaded(); return _userId; }
    }

    public bool IsStudent
    {
        get { EnsureLoaded(); return _isStudent; }
    }

    public Guid? OwnStructureNodeId
    {
        get { EnsureLoaded(); return _ownNodeId; }
    }

    public string? OwnStructureNodePath
    {
        get { EnsureLoaded(); return _ownPath; }
    }

    public IReadOnlySet<string> AuthorizedNodePaths
    {
        get { EnsureLoaded(); return _paths; }
    }

    public bool HasGlobalScope
    {
        get { EnsureLoaded(); return _global; }
    }

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        => LoadAsync(cancellationToken);

    private void EnsureLoaded()
    {
        if (!_loaded) LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_loaded) return;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_loaded) return;

            if (!_currentUser.IsAuthenticated)
            {
                _loaded = true;
                return;
            }

            _userId = _currentUser.Id;

            // Staff first, then Student — the JWT Id matches one of the two.
            var staff = await _dbContext.Staffs
                .AsNoTracking()
                .Where(s => s.Id == _userId)
                .Select(s => new { s.StructureNodeId })
                .FirstOrDefaultAsync(cancellationToken);

            if (staff is not null)
            {
                _isStudent = false;
                _ownNodeId = staff.StructureNodeId;
            }
            else
            {
                var student = await _dbContext.Students
                    .AsNoTracking()
                    .Where(s => s.Id == _userId)
                    .Select(s => new { s.StructureNodeId })
                    .FirstOrDefaultAsync(cancellationToken);
                if (student is not null)
                {
                    _isStudent = true;
                    _ownNodeId = student.StructureNodeId;
                }
            }

            if (_ownNodeId is Guid nodeId)
            {
                _ownPath = await _dbContext.StructureNodes
                    .AsNoTracking()
                    .Where(n => n.Id == nodeId)
                    .Select(n => n.Path)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            // Staff grants only — students don't use the authorized-paths set
            // because their visibility is computed relative to their own path
            // (see EffectiveScope).
            if (!_isStudent)
            {
                var grants = await _dbContext.StaffRoles
                    .AsNoTracking()
                    .Where(r => r.StaffId == _userId)
                    .Select(r => new { r.StructureNodeId, r.StructureNodePath })
                    .ToListAsync(cancellationToken);

                foreach (var g in grants)
                {
                    if (g.StructureNodeId is null)
                    {
                        _global = true;
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(g.StructureNodePath))
                    {
                        _paths.Add(g.StructureNodePath);
                    }
                }
            }

            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}

using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Semesters;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authorization;

public class ScopeResolver : IScopeResolver
{
    private readonly CoreDbContext _dbContext;
    private readonly IAcademicYearService _academicYearService;
    private readonly ISemesterService _semesterService;

    public ScopeResolver(CoreDbContext dbContext, IAcademicYearService academicYearService, ISemesterService semesterService)
    {
        _dbContext = dbContext;
        _academicYearService = academicYearService;
        _semesterService = semesterService;
    }
public async Task<AuthorizationScope> ResolveAsync(Guid userId, string year, string semester, Guid? structureNodeId = null, CancellationToken cancellationToken = default)
{
    var scope = new AuthorizationScope
    {
        Year = year,
        Semester = semester,
        StructureNodeId = structureNodeId
    };

        // 1. Resolve Temporal Scope (Current if requested)
        if (year == "Current")
        {
            var currentYear = await _academicYearService.GetCurrentAsync();
            scope.Year = currentYear?.Id.ToString() ?? ScopeKeys.Global;
        }

        if (semester == "Current")
        {
            var currentSem = await _semesterService.GetCurrentAsync();
            scope.Semester = currentSem?.Id.ToString() ?? ScopeKeys.Global;
        }

        // 2. Resolve Structural Scope
        // For students, the assigned StructureNodeId is authoritative — the request
        // context cannot widen or redirect it. For staff, the requested node (if any)
        // is later intersected against the user's grant paths by PermissionService.
        var studentTask = _dbContext.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == userId, cancellationToken);

        // Start node path resolution in parallel if already known from request.
        var nodeTask = scope.StructureNodeId.HasValue
            ? _dbContext.StructureNodes.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == scope.StructureNodeId.Value, cancellationToken)
            : null;

        var student = await studentTask;

        if (student != null)
        {
            scope.StructureNodeId = student.StructureNodeId;
        }

        if (scope.StructureNodeId.HasValue)
        {
            var node = nodeTask is not null
                ? await nodeTask
                : await _dbContext.StructureNodes.AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == scope.StructureNodeId, cancellationToken);
            scope.StructureNodePath = node?.Path;
        }

        return scope;
    }
}

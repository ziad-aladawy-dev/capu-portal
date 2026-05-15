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
public async Task<AuthorizationScope> ResolveAsync(Guid userId, string year, string semester, CancellationToken cancellationToken = default)
{
    var scope = new AuthorizationScope
    {
        Year = year,
        Semester = semester
    };

        // 1. Resolve Temporal Scope (Current if requested)
        if (year == "Current")
        {
            var currentYear = await _academicYearService.GetCurrentAsync();
            scope.Year = currentYear?.Id.ToString() ?? "Global";
        }

        if (semester == "Current")
        {
            var currentSem = await _semesterService.GetCurrentAsync();
            scope.Semester = currentSem?.Id.ToString() ?? "Global";
        }

        // 2. Resolve Structural Scope
        // Check if user is a Student
        var student = await _dbContext.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == userId, cancellationToken);

        if (student != null)
        {
            scope.StructureNodeId = student.StructureNodeId;
        }

        // If a node ID is resolved (or was provided), resolve its path
        if (scope.StructureNodeId.HasValue)
        {
            var node = await _dbContext.StructureNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == scope.StructureNodeId, cancellationToken);
            scope.StructureNodePath = node?.Path;
        }

        return scope;
    }
}

using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Queries;

public class GetRoleMembersRequest
{
    public Guid RoleId { get; set; }
}

public class RoleMemberResponse
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public Guid? StructureNodeId { get; set; }
    public string? StructureNodePath { get; set; }
    public string Year { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
}

public class GetRoleMembersQueryHandler
{
    private readonly CoreDbContext _dbContext;

    public GetRoleMembersQueryHandler(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<RoleMemberResponse>> Handle(GetRoleMembersRequest request, CancellationToken cancellationToken)
    {
        return await _dbContext.StaffRoles
            .AsNoTracking()
            .Where(sr => sr.RoleId == request.RoleId)
            .Join(_dbContext.Staffs.AsNoTracking(),
                sr => sr.StaffId,
                s => s.Id,
                (sr, s) => new RoleMemberResponse
                {
                    Id = sr.Id,
                    StaffId = s.Id,
                    Name = s.Name,
                    Email = s.Email,
                    EmployeeCode = s.EmployeeCode,
                    JobTitle = s.JobTitle,
                    StructureNodeId = sr.StructureNodeId,
                    StructureNodePath = sr.StructureNodePath,
                    Year = sr.Year,
                    Semester = sr.Semester,
                })
            .ToListAsync(cancellationToken);
    }
}

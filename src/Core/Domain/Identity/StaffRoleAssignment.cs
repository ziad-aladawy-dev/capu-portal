using CapitalUniversity.Core.Abstractions.Auth.Authorization;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

public class StaffRoleAssignment : BaseEntity, IUserRoleAssignment
{
    public Guid StaffId { get; set; }
    public Guid RoleId { get; private set; }
    public string Domain { get; private set; }
    public string Year { get; private set; }
    public string Semester { get; private set; }
    
    public Guid? FacultyId { get; set; }
    public Guid? ProgramId { get; set; }

    public StaffRoleAssignment(Guid staffId, Guid roleId, string domain, string year, string semester)
    {
        StaffId = staffId;
        RoleId = roleId;
        Domain = domain;
        Year = year;
        Semester = semester;
    }
}
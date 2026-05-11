using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authorization;

namespace CapitalUniversity.Core.Domain.Users;

public class StaffRoleAssignment : BaseEntity, IUserRoleAssignment
{
    public Guid StaffId { get; set; }
    public Guid RoleId { get; private set; }
    public Guid? UniversityId { get; private set; }
    public Guid? FacultyId { get; set; }
    public Guid? ProgramId { get; set; }
    public string Year { get; private set; }
    public string Semester { get; private set; }

    public StaffRoleAssignment(Guid staffId, Guid roleId, Guid? universityId, Guid? facultyId, Guid? programId, string year, string semester)
    {
        StaffId = staffId;
        RoleId = roleId;
        UniversityId = universityId;
        FacultyId = facultyId;
        ProgramId = programId;
        Year = year;
        Semester = semester;
    }
}
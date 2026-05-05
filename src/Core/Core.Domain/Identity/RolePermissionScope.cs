using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Identity;

public class RolePermissionScope : BaseEntity
{
    public Guid RolePermissionId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? ProgramId { get; set; }

    public RolePermission RolePermission { get; set; }
    public Faculty Faculty { get; set; }
    public AcademicProgram AcademicProgram { get; set; }
}
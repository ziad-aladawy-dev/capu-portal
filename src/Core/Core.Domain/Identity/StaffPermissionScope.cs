using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Identity;

public class StaffPermissionScope : BaseEntity
{
    public Guid StaffPermissionId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? ProgramId { get; set; }

    public StaffPermission StaffPermission { get; set; }
    public Faculty Faculty { get; set; }
    public AcademicProgram AcademicProgram { get; set; }
}
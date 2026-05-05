using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Identity;

public class StaffRole : BaseEntity
{
    public Guid StaffId { get; set; }
    public Guid RoleId { get; set; }
    public Guid? FacultyId { get; set; }   
    public Guid? ProgramId { get; set; } 

    public Staff Staff { get; set; }
    public Role Role { get; set; }
    public Faculty Faculty { get; set; }
    public AcademicProgram AcademicProgram { get; set; }
}
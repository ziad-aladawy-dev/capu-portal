using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.UniversityStructure;

public class FacultySystem : BaseEntity
{
    public Guid FacultyId { get; set; }
    public SystemTypeEnum SystemType { get; set; }

    public Faculty Faculty { get; set; } = null!;
    public ICollection<AcademicProgram> AcademicPrograms { get; set; } = new List<AcademicProgram>();
}
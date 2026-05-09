using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.UniversityStructure;

public class AcademicProgram : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid FacultyId { get; set; }
    public Guid FacultySystemId { get; set; }
    public ProgramTypeEnum ProgramType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public int? TotalHours { get; set; }

    public Guid? ParentId { get; set; }

    public FacultySystem FacultySystem { get; set; }
    public AcademicProgram Parent { get; set; }
    public ICollection<AcademicProgram> Children { get; set; } = new List<AcademicProgram>();

    public ICollection<Level> Levels { get; set; } = new List<Level>();
}
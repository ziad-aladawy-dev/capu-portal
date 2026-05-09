using CapitalUniversity.Core.Domain.Academic;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.UniversityStructure;

public class Level : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }        
    public Guid ProgramId { get; set; }
    public int TotalHours { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public AcademicProgram AcademicProgram { get; set; } = null!;
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Academic;

public class Course : BaseEntity
{
    public string CourseCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int? CreditHours { get; set; }
    public Guid? PrerequisiteCourseId { get; set; }
    public Guid? ConfilictingCourseId { get; set; }
    public Guid? LevelId { get; set; }

    public Course PrerequisiteCourse { get; set; } = null!;
    public Level Level { get; set; } = null!;
}
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Academic;

public class Course : BaseEntity
{
    public string CourseCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public Guid? PrerequisiteCourseId { get; set; }
    public Guid LevelId { get; set; }

    public virtual Course? PrerequisiteCourse { get; set; }
    public virtual Level Level { get; set; }
}
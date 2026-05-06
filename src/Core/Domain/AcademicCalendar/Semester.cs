using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.AcademicCalendar;

public class Semester : BaseEntity
{
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public AcademicYear AcademicYear { get; set; }
}
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.AcademicCalendar;

public class AcademicYear : BaseEntity
{
    public string Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public ICollection<Semester> Semesters { get; set; } = new List<Semester>();

}
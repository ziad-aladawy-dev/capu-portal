using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Semsters;

public class AcademicYear : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public ICollection<Semester> Semesters { get; set; } = new List<Semester>();
}

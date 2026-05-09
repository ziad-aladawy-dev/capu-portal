using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.UniversityStructure;

public class Faculty : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid UniversityId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public University University { get; set; }
    public ICollection<FacultySystem> Systems { get; set; } = new List<FacultySystem>();
}
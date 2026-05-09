using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.UniversityStructure;

public class University : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Faculty> Faculties { get; set; } = new List<Faculty>();
}
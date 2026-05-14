using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Identity;

public class Student : BaseEntity
{
    public string StudentCode { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public Guid StructureNodeId { get; set; }

    public StructureNode StructureNode { get; set; } = null!;

    public DateTime? PasswordExpiry { get; set; }

    public bool IsActive { get; set; } = true;
}
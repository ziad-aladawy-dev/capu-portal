using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Identity;

public class Student : BaseEntity
{
    public string StudentCode { get; set; }

    public string PasswordHash { get; set; }

    public string Name { get; set; }

    public string NationalId { get; set; }

    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; }

    public string Email { get; set; }

    public Guid StructureNodeId { get; set; }

    public StructureNode StructureNode { get; set; }

    public bool IsActive { get; set; }
    public DateTime? PasswordExpiry { get; set; }
}
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Identity;

public class Staff : BaseEntity
{
    public string EmployeeCode { get; set; }

    public string PasswordHash { get; set; }

    public string Name { get; set; }

    public string NationalId { get; set; }

    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; }

    public string Email { get; set; }

    public string Role { get; set; }

    public string JobTitle { get; set; }

    public Guid StructureNodeId { get; set; }

    public StructureNode StructureNode { get; set; }

    public bool IsActive { get; set; }

}

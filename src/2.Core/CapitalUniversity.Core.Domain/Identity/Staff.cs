using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Identity;

public class Staff : BaseEntity
{
    public string EmployeeCode { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;

    public Guid StructureNodeId { get; set; }

    public StructureNode StructureNode { get; set; } = null!;

    public DateTime? PasswordExpiry { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Monotonic counter bumped on logout / password change. Tokens carry the version
    /// snapshot at issue time; a mismatch with this row's current value invalidates
    /// the bearer, providing stateless-revocation without a token blocklist.
    /// </summary>
    public int SessionVersion { get; set; }
}
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Identity;

public class Staff : BaseEntity
{
    public string NationalId { get; set; } = string.Empty;
    public string StaffCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty; 
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public Guid UniversityId { get; set; }

    public University? University { get; set; }
    public ICollection<StaffRole> Roles { get; set; } = new List<StaffRole>();
    public ICollection<StaffPermission> DirectPermissions { get; set; } = new List<StaffPermission>();
}
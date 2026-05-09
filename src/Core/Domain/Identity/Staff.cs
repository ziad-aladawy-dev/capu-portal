using CapitalUniversity.Core.Domain.Common;


namespace CapitalUniversity.Core.Domain.Identity;

public class Staff : BaseEntity
{
    public string NationalId { get; set; } = string.Empty;
    public string StaffCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public DateTime? PasswordExpiry { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public Guid UniversityId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? ProgramId { get; set; }
}
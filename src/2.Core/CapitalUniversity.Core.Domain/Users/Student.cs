using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;
namespace CapitalUniversity.Core.Domain.Users;

public class Student : BaseEntity
{ 
    public string NationalId { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public DateTime? PasswordExpiry { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public DateTime EnrollmentDate { get; set; }
    public DateTime? GraduationDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid FacultyId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid? LevelId { get; set; }
    public Guid? CurrentAcademicYearId { get; set; }
    public Guid? CurrentSemesterId { get; set; }

    public StudentStatusEnum Status { get; set; }

}
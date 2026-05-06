using CapitalUniversity.Core.Domain.AcademicCalendar;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Identity;

public class Student : BaseEntity
{
    public string NationalId { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
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

    public Faculty Faculty { get; set; }
    public AcademicProgram AcademicProgram { get; set; }
    public Level Level { get; set; }
    public AcademicYear CurrentAcademicYear { get; set; }
    public Semester CurrentSemester { get; set; }
}
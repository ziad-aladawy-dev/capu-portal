namespace CapitalUniversity.Core.Abstractions.Students.DTOs;

public class StudentDto
{
    public Guid Id { get; set; }

    public string StudentCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string LocalizedName { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public string? Gender { get; set; }

    public string? GuardianName { get; set; }

    public string? GuardianPhone { get; set; }

    public Guid StructureNodeId { get; set; }

    public string StructureNodeName { get; set; } = string.Empty;

    public string FacultyName { get; set; } = string.Empty;

    public string ProgramName { get; set; } = string.Empty;

    public string LevelName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string PasswordStatus =>
        PasswordExpiry.HasValue &&
        PasswordExpiry < DateTime.UtcNow
            ? "Expired"
            : "Valid";

    public DateTime? PasswordExpiry { get; set; }

    public DateTime CreatedAt { get; set; }
}
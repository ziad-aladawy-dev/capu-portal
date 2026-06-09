namespace CapitalUniversity.Core.Abstractions.Students.DTOs;

public class UpdateStudentRequest
{
    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public string? Gender { get; set; }

    public string? GuardianName { get; set; }

    public string? GuardianPhone { get; set; }

    public Guid StructureNodeId { get; set; }

    public bool IsActive { get; set; }

    public string? Password { get; set; }

    public string? ConfirmPassword { get; set; }

    public DateTime? PasswordExpiry { get; set; }
}
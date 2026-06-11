namespace CapitalUniversity.Core.Abstractions.Students.DTOs;

/// <summary>
/// Partial update: null (omitted) fields keep the student's current value.
/// </summary>
public class UpdateStudentRequest
{
    public string? NameAr { get; set; }

    public string? NameEn { get; set; }

    public string? NationalId { get; set; }

    public DateTime? BirthDate { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? PhotoUrl { get; set; }

    public string? Gender { get; set; }

    public string? GuardianName { get; set; }

    public string? GuardianPhone { get; set; }

    public Guid? StructureNodeId { get; set; }

    public bool? IsActive { get; set; }

    public string? Password { get; set; }

    public string? ConfirmPassword { get; set; }

    public DateTime? PasswordExpiry { get; set; }
}

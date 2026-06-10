namespace CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;

public class CreateStaffRequest
{
    public string EmployeeCode { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public string? Gender { get; set; }

    public string? Qualification { get; set; }

    public Guid StructureNodeId { get; set; }

    public DateTime? PasswordExpiry { get; set; }
}
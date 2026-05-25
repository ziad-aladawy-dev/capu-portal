namespace CapitalUniversity.Core.Abstractions.Students.DTOs;

public class CreateStudentRequest
{
    public string StudentCode { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public Guid StructureNodeId { get; set; }

    public DateTime? PasswordExpiry { get; set; }
}
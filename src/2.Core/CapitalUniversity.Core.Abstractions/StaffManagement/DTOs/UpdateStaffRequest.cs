namespace CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;

public class UpdateStaffRequest
{
    public string Name { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;

    public Guid StructureNodeId { get; set; }

    public bool IsActive { get; set; }
}
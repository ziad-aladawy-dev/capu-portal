using CapitalUniversity.Core.Domain.StudentInformation;

namespace CapitalUniversity.Core.Abstractions.StudentInformation.DTOs;

public class StudentProfileRecordResponse
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public StudentProfileCategory Category { get; set; }
    public string CustomCategoryKey { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string DataJson { get; set; } = "{}";
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public bool IsSensitive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpsertStudentProfileRecordRequest
{
    public StudentProfileCategory Category { get; set; }
    /// <summary>Required when <see cref="Category"/> is <c>Custom</c>.</summary>
    public string? CustomCategoryKey { get; set; }
    public int SchemaVersion { get; set; }
    public string DataJson { get; set; } = "{}";
    public bool IsSensitive { get; set; }
}

public class VerifyStudentProfileRecordRequest
{
    public Guid VerifiedBy { get; set; }
}

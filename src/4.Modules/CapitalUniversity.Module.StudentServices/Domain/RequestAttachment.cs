using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class RequestAttachment : BaseEntity
{
    public Guid StudentRequestId { get; set; }
    public StudentRequest StudentRequest { get; set; } = null!;
    public string StepKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
}
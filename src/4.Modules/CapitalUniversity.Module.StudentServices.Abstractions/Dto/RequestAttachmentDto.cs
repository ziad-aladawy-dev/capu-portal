namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class RequestAttachmentDto
{
    public Guid Id { get; set; }
    public Guid StudentRequestId { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
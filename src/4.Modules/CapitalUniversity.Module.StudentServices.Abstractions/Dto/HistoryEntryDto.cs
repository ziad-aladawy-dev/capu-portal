namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class HistoryEntryDto
{
    public string Action { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public string? PerformedByRole { get; set; }
    public DateTime PerformedAt { get; set; }
}
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class StudentRequestDto
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public string? StudentName { get; set; }
    public RequestStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal? AmountPaid { get; set; }
    public int CurrentStepOrder { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RequestNumber { get; set; }
    public decimal? ServicePrice { get; set; }
    public List<HistoryEntryDto> History { get; set; } = new();
}
using CapitalUniversity.Modules.Payments.Abstractions;

namespace CapitalUniversity.Modules.Payments.Abstractions.DTOs;

public class InvoiceResponse
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public InvoiceStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<InvoiceItemResponse> Items { get; set; } = new();
}

public class InvoiceItemResponse
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string FeeType { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CreateInvoiceRequest
{
    public Guid StudentId { get; set; }
    public string Currency { get; set; } = "EGP";
    public DateTime? DueAt { get; set; }
    public List<CreateInvoiceItemRequest> Items { get; set; } = new();
}

public class CreateInvoiceItemRequest
{
    public decimal Amount { get; set; }
    public string FeeType { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CancelInvoiceRequest
{
    public string Reason { get; set; } = string.Empty;
}

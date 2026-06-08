namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

public class StudentFeeResponse
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid ReceiptId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public FeeStatus Status { get; set; }
    public string SourceModule { get; set; } = string.Empty;
    public Guid? SourceReferenceId { get; set; }
    public Guid? OrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderResponse
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public OrderStatus Status { get; set; }
    public Gateway Gateway { get; set; }
    public string MerchantOrderId { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public DateTime CreatedAt { get; set; }
    public List<StudentFeeResponse> Fees { get; set; } = new();
}

public class CreateOrderRequest
{
    public Guid StudentId { get; set; }
    public List<Guid> FeeIds { get; set; } = new();
    public Gateway Gateway { get; set; }
}

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

public class ServiceReceiptMappingResponse
{
    public Guid Id { get; set; }
    public Guid StudentServiceId { get; set; }
    public Guid ReceiptId { get; set; }
    public string ReceiptName { get; set; } = string.Empty;
    public decimal UnitAmount { get; set; }
    public string Currency { get; set; } = "EGP";
    public QuantityMode QuantityMode { get; set; }
    public bool IsActive { get; set; }
}

public class CreateServiceReceiptMappingRequest
{
    public Guid StudentServiceId { get; set; }
    public Guid ReceiptId { get; set; }
    public QuantityMode QuantityMode { get; set; } = QuantityMode.Fixed;
}

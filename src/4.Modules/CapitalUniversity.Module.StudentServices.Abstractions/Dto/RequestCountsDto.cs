namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class RequestCountsDto
{
    public int Draft { get; set; }
    public int Pending { get; set; }
    public int UnderReview { get; set; }
    public int MoreInfoRequired { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int PaymentPending { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int ReadyForPickup { get; set; }

    public int Total => Draft + Pending + UnderReview + MoreInfoRequired + Approved + Rejected + PaymentPending + Completed + Cancelled + ReadyForPickup;
}

namespace CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

public enum StepInputType
{
    Text = 1,
    TextArea = 2,
    Number = 3,
    Date = 4,
    FileUpload = 5,
    MultipleChoice = 6,
    Checkbox = 7
}

public enum RequestStatus
{
    Draft = 1,
    Pending = 2,
    UnderReview = 3,
    MoreInfoRequired = 4,
    Approved = 5,
    Rejected = 6,
    PaymentPending = 7,
    Completed = 8,
    Cancelled = 9,
    ReadyForPickup = 10
}

public enum PaymentStatus
{
    NotRequired = 1,
    Pending = 2,
    Paid = 3,
    Failed = 4,
    Refunded = 5
}
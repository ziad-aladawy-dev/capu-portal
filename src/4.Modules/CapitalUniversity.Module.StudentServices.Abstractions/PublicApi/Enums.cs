namespace CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

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

public enum ServiceType
{
    General = 1,
    Specialized = 2,
    Administrative = 3
}

public enum StepFieldType
{
    Text = 1,
    TextArea = 2,
    Number = 3,
    Date = 4,
    Select = 5,
    MultiSelect = 6,
    File = 7,
    Checkbox = 8,
    Radio = 9
}

public enum WorkflowStepType
{
    Form = 1,
    FileUpload = 2,
    Review = 3,
    Payment = 4,
    Submit = 5
}
namespace CapitalUniversity.Modules.StudentServices.Abstractions;

/// <summary>
/// Canonical lifecycle status for a <c>StudentServiceRequest</c>. The values are
/// stable and never reordered — persisted as int. Configurable
/// <c>WorkflowDefinition</c>s decide which transitions are permitted between
/// these statuses; the statuses themselves are universal so list filters and
/// query handlers can reason about them without consulting a workflow row.
/// </summary>
public enum ServiceRequestStatus
{
    Draft           = 0,
    Submitted       = 1,
    WaitingPayment  = 2,
    UnderReview     = 3,
    Approved        = 4,
    Rejected        = 5,
    Completed       = 6,
    Cancelled       = 7,
}

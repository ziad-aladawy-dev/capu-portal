namespace CapitalUniversity.Modules.CourseOffering.Abstractions;

/// <summary>
/// Lifecycle stage of an offering. Stored as int — values are stable and never
/// reordered. New values are additive.
/// </summary>
public enum OfferingStatus
{
    Draft = 0,
    Open = 1,
    Closed = 2,
    Cancelled = 3,
}

namespace CapitalUniversity.Modules.CourseOffering.Abstractions;

/// <summary>
/// Whether the offering is currently accepting registrations. Separate from
/// <see cref="OfferingStatus"/> so an offering can be <c>Open</c> (lifecycle)
/// but temporarily not accepting new students (e.g. capacity reached → waitlist,
/// or an administrative freeze) without losing its lifecycle stage.
///
/// <para>
/// The CourseOffering module owns the state value only; the rules that move
/// between states belong to a future Registration module.
/// </para>
/// </summary>
public enum RegistrationState
{
    Closed = 0,
    Open = 1,
    Waitlist = 2,
}

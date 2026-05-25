namespace CapitalUniversity.Modules.StudentServices.Abstractions;

/// <summary>
/// How a <c>WorkflowTransition</c> is fired. Stored as int — values are stable
/// and never reordered.
/// </summary>
public enum WorkflowTransitionType
{
    /// <summary>Triggered by an explicit staff/admin action — gated by <c>RequiredAction</c>.</summary>
    Manual    = 0,

    /// <summary>Fired by the system after an external event (e.g. payment completion).</summary>
    Automatic = 1,

    /// <summary>Triggered by the student (e.g. submit, cancel).</summary>
    Student   = 2,
}

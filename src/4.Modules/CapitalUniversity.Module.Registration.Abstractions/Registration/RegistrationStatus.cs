namespace CapitalUniversity.Modules.Registration.Abstractions;

/// <summary>
/// Lifecycle of a single course-registration attempt as reported by the
/// upstream academic system. The portal never transitions these — it only
/// stores the snapshot the Sync Platform delivers (see
/// <c>docs/Courses/Registration_Model.md</c>). Stored as <c>int</c>; values are
/// stable and never reordered, new values are additive.
///
/// <para>
/// <see cref="Enrolled"/> is the only "currently active" status —
/// <c>GetRegisteredCourses</c> filters on it. Everything else is historical and
/// surfaces through the history / attempts queries.
/// </para>
/// </summary>
public enum RegistrationStatus
{
    /// <summary>Active enrollment — the student is currently taking the course.</summary>
    Enrolled = 0,

    /// <summary>Finished — a terminal grade was recorded (pass).</summary>
    Completed = 1,

    /// <summary>Student withdrew/dropped after the registration was recorded.</summary>
    Withdrawn = 2,

    /// <summary>Finished with a failing outcome — the attempt counts toward repeat history.</summary>
    Failed = 3,

    /// <summary>Registration was cancelled upstream before it took effect.</summary>
    Cancelled = 4,
}

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

/// <summary>
/// Sentinel values used in the persisted string-typed scope columns
/// (<c>StaffRoleAssignment.Year</c> / <c>.Semester</c>,
/// <c>StaffPermissionOverride.Year</c> / <c>.Semester</c>) and in the
/// permission-lookup cache keys. Centralised here so a writer/reader
/// typo becomes a compile error instead of a silent filter miss.
///
/// <para>
/// A row whose temporal column equals <see cref="Global"/> matches every
/// concrete year/semester guid at filter time. Any other value is expected
/// to be a guid string (an <c>AcademicYear.Id.ToString()</c> or
/// <c>Semester.Id.ToString()</c>). The persisted column type is intentionally
/// left as <c>string</c> rather than a real value object — switching the
/// column requires a coupled schema migration + data conversion that's out
/// of scope for the constants refactor.
/// </para>
/// </summary>
public static class ScopeKeys
{
    /// <summary>The sentinel "matches every year / semester".</summary>
    public const string Global = "Global";

    /// <summary>
    /// Maps an optional guid to its string form, falling back to <see cref="Global"/>
    /// when the caller has no concrete scope to bind against.
    /// </summary>
    public static string FromGuid(System.Guid? id) =>
        id?.ToString() ?? Global;

    /// <summary>True if the persisted value represents the global sentinel.</summary>
    public static bool IsGlobal(string? value) =>
        string.Equals(value, Global, System.StringComparison.Ordinal);
}

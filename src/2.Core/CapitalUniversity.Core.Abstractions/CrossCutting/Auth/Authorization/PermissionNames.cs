namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

/// <summary>
/// Compile-time constants for every permission string the controllers + handlers
/// pass to <c>HasPermissionAttribute</c>. Each value is the canonical
/// <c>module.resource.action</c> form that <see cref="PermissionIdentity.Parse"/>
/// produces, so it round-trips identically against seeded role permissions and
/// the cached permission lookup.
///
/// <para>
/// Adding or renaming a permission anywhere in the codebase must go through a
/// constant here. The <c>PermissionNamesCoverageTests</c> contract test scans
/// the source for any string literal passed to <c>[HasPermission(...)]</c> that
/// is not a value defined on this class and fails the build.
/// </para>
/// </summary>
public static class PermissionNames
{
    /// <summary>
    /// Granted by the seeder's "View/Manage Permissions" service rows.
    /// Module = <c>permissions</c>, Resource = <c>permissions</c>.
    /// </summary>
    public static class Permissions
    {
        public const string View      = "permissions.permissions.View";
        public const string Insert    = "permissions.permissions.Insert";
        public const string EditClose = "permissions.permissions.EditClose";
        public const string Open      = "permissions.permissions.Open";
        public const string Delete    = "permissions.permissions.Delete";
    }

    /// <summary>
    /// Granted by the seeder's "Manage Roles" service row.
    /// Module = <c>permissions</c>, Resource = <c>roles</c>.
    /// </summary>
    public static class Roles
    {
        public const string View      = "permissions.roles.View";
        public const string Insert    = "permissions.roles.Insert";
        public const string EditClose = "permissions.roles.EditClose";
        public const string Open      = "permissions.roles.Open";
        public const string Delete    = "permissions.roles.Delete";
    }

    /// <summary>
    /// Used by <c>AcademicYearsController</c>. Currently uses the legacy
    /// <c>Module = Academic</c> / <c>Resource = Year</c> naming — the production
    /// seeder doesn't grant these (it grants <c>academics.academic-years.*</c> via
    /// the "View Academic Years" service), so this is effectively dead in prod
    /// until the controllers migrate to the canonical names. Keeping the constants
    /// here so the existing tests + bespoke seeded scenarios compile.
    /// </summary>
    public static class AcademicYear
    {
        public const string View      = "Academic.Year.View";
        public const string Insert    = "Academic.Year.Insert";
        public const string EditClose = "Academic.Year.EditClose";
        public const string Open      = "Academic.Year.Open";
        public const string Delete    = "Academic.Year.Delete";
    }

    /// <summary>
    /// Used by <c>SemestersController</c>. Same legacy-naming note as
    /// <see cref="AcademicYear"/>.
    /// </summary>
    public static class AcademicSemester
    {
        public const string View      = "Academic.Semester.View";
        public const string Insert    = "Academic.Semester.Insert";
        public const string EditClose = "Academic.Semester.EditClose";
        public const string Open      = "Academic.Semester.Open";
        public const string Delete    = "Academic.Semester.Delete";
    }
}

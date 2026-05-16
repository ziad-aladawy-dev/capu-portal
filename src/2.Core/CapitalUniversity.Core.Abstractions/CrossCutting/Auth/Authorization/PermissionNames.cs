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
    /// Combined academic-timeline permissions covering BOTH academic years and
    /// semesters. The system already collapses these into a single resource at
    /// runtime — <see cref="PermissionIdentity.ResourceFor"/> maps every academics-
    /// module service to the <c>academic-years</c> resource — so granting one set
    /// of permissions on this resource grants management of both tables. This is
    /// intentional: anyone with academic temporal scope management needs both.
    ///
    /// <para>
    /// Module = <c>academics</c>, Resource = <c>academic-years</c>. Bound by
    /// <see cref="AcademicYearsController"/> and <see cref="SemestersController"/>.
    /// Declared by <c>AcademicsPermissionManifest</c>; the seeder grants this via
    /// the "Academic Timeline" service row.
    /// </para>
    /// </summary>
    public static class AcademicTimeline
    {
        public const string View      = "academics.academic-years.View";
        public const string Insert    = "academics.academic-years.Insert";
        public const string EditClose = "academics.academic-years.EditClose";
        public const string Open      = "academics.academic-years.Open";
        public const string Delete    = "academics.academic-years.Delete";
    }
}

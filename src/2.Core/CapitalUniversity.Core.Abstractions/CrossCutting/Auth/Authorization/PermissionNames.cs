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
    /// semesters. Modelled as a single resource (<c>academic-years</c>) because
    /// anyone with academic temporal scope management needs both tables, not one.
    ///
    /// <para>
    /// Module = <c>academics</c>, Resource = <c>academic-years</c>. Bound by
    /// <see cref="AcademicYearsController"/> and <see cref="SemestersController"/>.
    /// Declared by <c>AcademicsPermissionManifest</c>; the seeder grants this via
    /// the "Academic Timeline" resource row.
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

    /// <summary>
    /// Course catalog management (catalog-only, no registration/enrollment
    /// concerns — those belong to a future Registration module).
    /// Module = <c>courses</c>, Resource = <c>courses</c>. Bound by
    /// <see cref="CoursesController"/>; declared by <c>CoursesPermissionManifest</c>.
    /// </summary>
    public static class Courses
    {
        public const string View      = "courses.courses.View";
        public const string Insert    = "courses.courses.Insert";
        public const string EditClose = "courses.courses.EditClose";
        public const string Open      = "courses.courses.Open";
        public const string Delete    = "courses.courses.Delete";
    }

    /// <summary>
    /// Curriculum composition (which catalog courses belong to which plan at
    /// which level/semester). Module = <c>courses</c>, Resource = <c>academic-plans</c>.
    /// Bound by <see cref="AcademicPlansController"/>; declared by
    /// <c>CoursesPermissionManifest</c>.
    /// </summary>
    public static class AcademicPlans
    {
        public const string View      = "courses.academic-plans.View";
        public const string Insert    = "courses.academic-plans.Insert";
        public const string EditClose = "courses.academic-plans.EditClose";
        public const string Open      = "courses.academic-plans.Open";
        public const string Delete    = "courses.academic-plans.Delete";
    }

    /// <summary>
    /// Invoice lifecycle (create / read / cancel). Module = <c>payments</c>,
    /// Resource = <c>invoices</c>. Bound by <see cref="InvoicesController"/>;
    /// declared by <c>PaymentsPermissionManifest</c>.
    /// </summary>
    public static class Invoices
    {
        public const string View      = "payments.invoices.View";
        public const string Insert    = "payments.invoices.Insert";
        public const string EditClose = "payments.invoices.EditClose";
        public const string Open      = "payments.invoices.Open";
        public const string Delete    = "payments.invoices.Delete";
    }

    /// <summary>
    /// Payment provider transactions (record / view). Webhook handlers should
    /// hold this without holding <see cref="Invoices"/>.
    /// </summary>
    public static class PaymentTransactions
    {
        public const string View      = "payments.transactions.View";
        public const string Insert    = "payments.transactions.Insert";
        public const string EditClose = "payments.transactions.EditClose";
        public const string Open      = "payments.transactions.Open";
        public const string Delete    = "payments.transactions.Delete";
    }

    /// <summary>
    /// Student Information profile records (sparse, JSON-backed sensitive data).
    /// Module = <c>student-information</c>, Resource = <c>profile-records</c>.
    /// Bound by <see cref="StudentProfileRecordsController"/>; declared by
    /// <c>StudentInformationPermissionManifest</c>.
    /// </summary>
    public static class StudentProfileRecords
    {
        public const string View      = "student-information.profile-records.View";
        public const string Insert    = "student-information.profile-records.Insert";
        public const string EditClose = "student-information.profile-records.EditClose";
        public const string Open      = "student-information.profile-records.Open";
        public const string Delete    = "student-information.profile-records.Delete";
    }

    /// <summary>
    /// Course offering lifecycle (schedule sections per term + structure node).
    /// Module = <c>course-offerings</c>, Resource = <c>course-offerings</c>.
    /// Bound by <see cref="CourseOfferingsController"/>; declared by
    /// <c>CourseOfferingPermissionManifest</c>.
    /// </summary>
    public static class CourseOfferings
    {
        public const string View      = "course-offerings.course-offerings.View";
        public const string Insert    = "course-offerings.course-offerings.Insert";
        public const string EditClose = "course-offerings.course-offerings.EditClose";
        public const string Open      = "course-offerings.course-offerings.Open";
        public const string Delete    = "course-offerings.course-offerings.Delete";
    }

}

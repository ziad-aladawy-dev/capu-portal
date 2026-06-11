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
    /// Student self-service payment orders (B8). Distinct from the ops
    /// <see cref="PaymentTransactions"/> resource: a student holds these
    /// IMPLICITLY (<c>StudentSelfPermissions</c>) to view their own fees and
    /// create/initiate orders for themselves; staff/ops receive them via the
    /// Super Admin grant-all. Self-access is enforced in OrderService
    /// (CanAccessStudentAsync on every operation). Declared by
    /// <c>PaymentsPermissionManifest</c> (resource <c>orders</c>).
    /// </summary>
    public static class PaymentOrders
    {
        public const string View      = "payments.orders.View";
        public const string Insert    = "payments.orders.Insert";
        public const string EditClose = "payments.orders.EditClose";
        public const string Open      = "payments.orders.Open";
        public const string Delete    = "payments.orders.Delete";
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

    public static class CourseOfferings
    {
        public const string View      = "course-offerings.course-offerings.View";
        public const string Insert    = "course-offerings.course-offerings.Insert";
        public const string EditClose = "course-offerings.course-offerings.EditClose";
        public const string Open      = "course-offerings.course-offerings.Open";
        public const string Delete    = "course-offerings.course-offerings.Delete";
    }

    /// <summary>
    /// Registered Courses — read-only access to course registrations synced from
    /// external academic systems. Students hold View (own-row scope enforced in
    /// the service layer via <c>IEffectiveScope</c>); staff read within their
    /// structure-node scope. Module = <c>registered-courses</c>, Resource =
    /// <c>registered-courses</c>. Bound by <see cref="RegisteredCoursesController"/>;
    /// declared by <c>RegistrationPermissionManifest</c>.
    /// </summary>
    public static class RegisteredCourses
    {
        public const string View      = "registered-courses.registered-courses.View";
        public const string Insert    = "registered-courses.registered-courses.Insert";
        public const string EditClose = "registered-courses.registered-courses.EditClose";
        public const string Open      = "registered-courses.registered-courses.Open";
        public const string Delete    = "registered-courses.registered-courses.Delete";
    }

    /// <summary>
    /// Academic Records — Grades. Read-only access to a student's synchronized
    /// grade history, semester detail, and academic summary. Students hold View
    /// (own-row scope enforced in the service layer via <c>IEffectiveScope</c>);
    /// staff read within their structure-node scope. Module =
    /// <c>academic-records</c>, Resource = <c>grades</c>. Bound by
    /// <see cref="GradesController"/>; declared by <c>AcademicRecordsPermissionManifest</c>.
    /// </summary>
    public static class Grades
    {
        public const string View      = "academic-records.grades.View";
        public const string Insert    = "academic-records.grades.Insert";
        public const string EditClose = "academic-records.grades.EditClose";
        public const string Open      = "academic-records.grades.Open";
        public const string Delete    = "academic-records.grades.Delete";
    }

    /// <summary>
    /// Academic Records — Transcript. Read-only access to a student's transcript
    /// structure + PDF export. Same own-row / structure-node scope rules as
    /// <see cref="Grades"/>. Module = <c>academic-records</c>, Resource =
    /// <c>transcript</c>. Bound by <see cref="TranscriptController"/>; declared by
    /// <c>AcademicRecordsPermissionManifest</c>.
    /// </summary>
    public static class Transcript
    {
        public const string View      = "academic-records.transcript.View";
        public const string Insert    = "academic-records.transcript.Insert";
        public const string EditClose = "academic-records.transcript.EditClose";
        public const string Open      = "academic-records.transcript.Open";
        public const string Delete    = "academic-records.transcript.Delete";
    }

    public static class Schedule
    {
        public const string View      = "schedule.schedule-slots.View";
        public const string Insert    = "schedule.schedule-slots.Insert";
        public const string EditClose = "schedule.schedule-slots.EditClose";
        public const string Open      = "schedule.schedule-slots.Open";
        public const string Delete    = "schedule.schedule-slots.Delete";
    }

    /// <summary>
    /// Student Services — service catalog management (admin only).
    /// Module = <c>student-services</c>, Resource = <c>services</c>.
    /// </summary>
    public static class StudentServicesCatalog
    {
        public const string View      = "student-services.services.View";
        public const string Insert    = "student-services.services.Insert";
        public const string EditClose = "student-services.services.EditClose";
        public const string Open      = "student-services.services.Open";
        public const string Delete    = "student-services.services.Delete";
    }

    /// <summary>
    /// Student Services — request lifecycle. Students hold View+Insert through
    /// their default role grant (own-row scope enforced in the service layer);
    /// staff hold the higher verbs.
    /// Module = <c>student-services</c>, Resource = <c>requests</c>.
    /// </summary>
    public static class StudentServiceRequests
    {
        public const string View      = "student-services.requests.View";
        public const string Insert    = "student-services.requests.Insert";
        public const string EditClose = "student-services.requests.EditClose";
        public const string Open      = "student-services.requests.Open";
        public const string Delete    = "student-services.requests.Delete";
    }

    /// <summary>
    /// Student Services — workflow configuration (admin only).
    /// Module = <c>student-services</c>, Resource = <c>workflows</c>.
    /// </summary>
    public static class StudentServiceWorkflows
    {
        public const string View      = "student-services.workflows.View";
        public const string Insert    = "student-services.workflows.Insert";
        public const string EditClose = "student-services.workflows.EditClose";
        public const string Open      = "student-services.workflows.Open";
        public const string Delete    = "student-services.workflows.Delete";
    }

    /// <summary>
    /// System audit trail (read-only admin view of the Mongo audit log).
    /// Module = <c>system</c>, Resource = <c>audit-logs</c>. Bound by
    /// <see cref="AuditLogsController"/>; declared by <c>SystemPermissionManifest</c>.
    /// Granted to Super Admin via the seeder's "every resource" loop.
    /// </summary>
    public static class AuditLogs
    {
        public const string View = "system.audit-logs.View";
    }

    public static class StudentServices
    {
        // Services
        public const string ServicesView = "student-services.services.View";
        public const string ServicesInsert = "student-services.services.Insert";
        public const string ServicesEditClose = "student-services.services.EditClose";
        public const string ServicesOpen = "student-services.services.Open";
        public const string ServicesDelete = "student-services.services.Delete";

        // Requests
        public const string RequestsView = "student-services.requests.View";
        public const string RequestsInsert = "student-services.requests.Insert";
        public const string RequestsEditClose = "student-services.requests.EditClose";
        public const string RequestsOpen = "student-services.requests.Open";
        public const string RequestsDelete = "student-services.requests.Delete";
        public const string RequestsAssign = "student-services.requests.Assign";

        // Workflows
        public const string WorkflowsView = "student-services.workflows.View";
        public const string WorkflowsInsert = "student-services.workflows.Insert";
        public const string WorkflowsEditClose = "student-services.workflows.EditClose";
        public const string WorkflowsOpen = "student-services.workflows.Open";
        public const string WorkflowsDelete = "student-services.workflows.Delete";
    }
}

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

/// <summary>
/// Compile-time constants for every key the infrastructure-layer code passes to
/// <see cref="ILocalizationService.GetString"/>. Strings live in
/// <c>LocalizedStrings</c> per culture; the indirection keeps controllers and
/// middleware out of the raw literal business.
///
/// <para>
/// Naming: <c>{Area}.{Reason}</c>. Areas cover both cross-cutting infrastructure
/// (Auth, Permissions, Infrastructure validation) AND owned business modules
/// (Courses, Semesters, Payments, StudentInformation) per the Runtime Hardening
/// Plan section 2.
/// </para>
/// </summary>
public static class LocalizedKeys
{
    public static class Auth
    {
        public const string Unauthorized       = "auth.unauthorized";
        public const string InvalidCredentials = "auth.invalid_credentials";
        public const string SessionExpired     = "auth.session_expired";
        public const string TokenInvalid       = "auth.token_invalid";
        public const string PasswordChangeFailed = "auth.password_change_failed";
    }

    public static class Permissions
    {
        public const string Forbidden = "permissions.forbidden";
    }

    public static class Infrastructure
    {
        public const string ValidationError = "infrastructure.validation_error";
        public const string NotFound        = "infrastructure.not_found";
        public const string Conflict        = "infrastructure.conflict";
        public const string ServerError     = "infrastructure.server_error";
        public const string DuplicateKey    = "infrastructure.duplicate_key";
        public const string ForeignKeyViolation = "infrastructure.foreign_key_violation";
    }

    public static class Courses
    {
        public const string NotFound                  = "courses.not_found";
        public const string CodeInUse                 = "courses.code_in_use";
        public const string CreditHoursOutOfRange     = "courses.credit_hours_out_of_range";
        public const string PlanNotFound              = "courses.plan_not_found";
        public const string PlanCourseEntryNotFound   = "courses.plan_course_entry_not_found";
        public const string PlanCourseAlreadyPresent  = "courses.plan_course_already_present";
        public const string StructureNodeNotFound     = "courses.structure_node_not_found";
        public const string EffectiveToAfterFrom      = "courses.effective_to_after_from";
    }

    public static class Semesters
    {
        public const string NotFound                  = "semesters.not_found";
        public const string AcademicYearNotFound      = "semesters.academic_year_not_found";
        public const string AcademicYearMissing       = "semesters.academic_year_missing";
        public const string DatesOverlap              = "semesters.dates_overlap";
        public const string DatesOutsideAcademicYear  = "semesters.dates_outside_academic_year";
        public const string EndAfterStart             = "semesters.end_after_start";
        public const string YearDatesOverlap          = "semesters.year_dates_overlap";
    }

    public static class Payments
    {
        public const string InvoiceNotFound           = "payments.invoice_not_found";
        public const string StudentNotFound           = "payments.student_not_found";
        public const string AtLeastOneItem            = "payments.at_least_one_item";
        public const string PaidCannotCancel          = "payments.paid_cannot_cancel";
    }

    public static class StudentInformation
    {
        public const string ProfileRecordNotFound = "studentinfo.profile_record_not_found";
        public const string StudentNotFound       = "studentinfo.student_not_found";
        public const string InvalidJson           = "studentinfo.invalid_json";
        public const string CustomCategoryKeyRequired = "studentinfo.custom_category_key_required";
    }

    public static class CourseOfferings
    {
        public const string NotFound                 = "course_offerings.not_found";
        public const string SectionInUse             = "course_offerings.section_in_use";
        public const string CapacityNegative         = "course_offerings.capacity_negative";
        public const string CapacityBelowCount       = "course_offerings.capacity_below_count";
        public const string SectionCodeRequired      = "course_offerings.section_code_required";
        public const string IllegalStateTransition   = "course_offerings.illegal_state_transition";
    }

    public static class Schedule
    {
        public const string NotFound        = "schedule.not_found";
        public const string DuplicateSlot   = "schedule.duplicate_slot";
        public const string EndBeforeStart  = "schedule.end_before_start";
        public const string SlotConflict    = "schedule.slot_conflict";
    }
}

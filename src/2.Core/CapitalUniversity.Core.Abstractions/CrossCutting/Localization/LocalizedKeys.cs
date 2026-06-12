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

        /// <summary>
        /// Human-readable descriptions for the CRUD verbs that appear in the
        /// permission-tree response. Keyed by the <c>ActionDefinition.Name</c>
        /// value (case-sensitive). The unknown-action fallback is
        /// <see cref="ActionDescriptionFallback"/>.
        /// </summary>
        public static class ActionDescription
        {
            public const string View      = "permissions.action_description.view";
            public const string Insert    = "permissions.action_description.insert";
            public const string EditClose = "permissions.action_description.edit_close";
            public const string Open      = "permissions.action_description.open";
            public const string Delete    = "permissions.action_description.delete";
        }

        /// <summary>
        /// Generic description used when an action name has no dedicated
        /// catalog entry (e.g. module-defined <c>Approve</c>, <c>Export</c>).
        /// The placeholder is filled in at the call site, not by the
        /// catalog — see <c>PermissionTreeQueryHandler.DescribeAction</c>.
        /// </summary>
        public const string ActionDescriptionFallback = "permissions.action_description.generic";
    }

    public static class Infrastructure
    {
        public const string ValidationError = "infrastructure.validation_error";
        public const string NotFound        = "infrastructure.not_found";
        public const string Conflict        = "infrastructure.conflict";
        public const string ServerError     = "infrastructure.server_error";
        public const string DuplicateKey    = "infrastructure.duplicate_key";
        public const string ForeignKeyViolation = "infrastructure.foreign_key_violation";
        public const string Required        = "infrastructure.required";
        public const string Invalid         = "infrastructure.invalid";
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
        public const string ReferencedByPlan          = "courses.referenced_by_plan";
        public const string PrerequisiteNotFound      = "courses.prerequisite_not_found";
        public const string PrerequisiteSelfReference = "courses.prerequisite_self_reference";
        public const string PrerequisiteCycle         = "courses.prerequisite_cycle";
        public const string PrerequisiteAlreadyExists = "courses.prerequisite_already_exists";
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
        public const string CodeInUse             = "studentinfo.code_in_use";
        public const string EmailInUse            = "studentinfo.email_in_use";
        public const string NationalIdInUse       = "studentinfo.national_id_in_use";
        public const string PasswordsDoNotMatch   = "studentinfo.passwords_do_not_match";
        public const string StructureNodeNotFound = "studentinfo.structure_node_not_found";
        public const string MustBeLevelNode       = "studentinfo.must_be_level_node";
    }

    public static class StaffManagement
    {
        public const string StaffNotFound         = "staff.not_found";
        public const string CodeInUse             = "staff.code_in_use";
        public const string EmailInUse            = "staff.email_in_use";
        public const string NationalIdInUse       = "staff.national_id_in_use";
        public const string PasswordsDoNotMatch   = "staff.passwords_do_not_match";
        public const string StructureNodeNotFound = "staff.structure_node_not_found";
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

    /// <summary>
    /// Cross-cutting validation messages for bulk-action endpoints (Phase 1 /
    /// Phase 3). Used by <c>BulkRequestGuard</c> when a controller-level
    /// guard fires before the per-row service work runs.
    /// </summary>
    public static class BulkActions
    {
        public const string IdsRequired         = "bulk_actions.ids_required";
        public const string BatchExceedsMaxSize = "bulk_actions.batch_exceeds_max_size";
        public const string PayloadRequired     = "bulk_actions.payload_required";
        public const string ReasonRequired      = "bulk_actions.reason_required";
        public const string RecordsRequired     = "bulk_actions.records_required";
        public const string VerifiedByRequired  = "bulk_actions.verified_by_required";
    }

    /// <summary>
    /// Cross-cutting validation messages for paged-query / search endpoints
    /// (Phase 2). Produced by <c>SortClause.Parse</c> when the caller asks
    /// for an unsupported sort field or direction.
    /// </summary>
    public static class Paging
    {
        public const string UnknownSortField     = "paging.unknown_sort_field";
        public const string UnknownSortDirection = "paging.unknown_sort_direction";
    }

    public static class StudentServices
    {
        public const string ServiceNotFound        = "student_services.service_not_found";
        public const string ServiceInactive        = "student_services.service_inactive";
        public const string ServiceCodeInUse       = "student_services.service_code_in_use";
        public const string RequestNotFound        = "student_services.request_not_found";
        public const string RequiredFieldMissing   = "student_services.required_field_missing";
        public const string RequiredDocumentMissing = "student_services.required_document_missing";
        public const string InvalidFieldValue      = "student_services.invalid_field_value";
        public const string InvalidDropdownValue   = "student_services.invalid_dropdown_value";
        public const string InvalidFileExtension   = "student_services.invalid_file_extension";
        public const string FileTooLarge           = "student_services.file_too_large";
        public const string InvalidTransition      = "student_services.invalid_transition";
        public const string UnauthorizedTransition = "student_services.unauthorized_transition";
        public const string CannotCancelAfterProcessing = "student_services.cannot_cancel_after_processing";
        public const string WorkflowNotFound       = "student_services.workflow_not_found";
        public const string WorkflowCodeInUse      = "student_services.workflow_code_in_use";
        public const string WorkflowMissingInitial = "student_services.workflow_missing_initial";
        public const string PaymentRequired        = "student_services.payment_required";
        public const string RequestNotWaitingPayment = "student_services.request_not_waiting_payment";
        public const string DuplicateFieldValue    = "student_services.duplicate_field_value";
    }
}

using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Core.Application.CrossCutting.Localization;

/// <summary>
/// In-memory translation table for the keys catalogued in <see cref="LocalizedKeys"/>.
///
/// <para>
/// Two cultures shipped: <c>"ar"</c> (default per <see cref="LocalizationService"/>)
/// and <c>"en"</c>. Adding a third culture means adding another inner dictionary
/// here — no file IO, no resx tooling. The intent is to keep cross-cutting
/// translations close to the keys so a missed entry is obvious in code review.
/// </para>
///
/// <para>
/// Out-of-catalogue keys fall through to <c>key</c> itself, so a forgotten
/// translation degrades to a readable identifier rather than throwing or
/// returning empty.
/// </para>
/// </summary>
public static class LocalizedStrings
{
    public const string DefaultCulture = "ar";

    private static readonly Dictionary<string, Dictionary<string, string>> Table =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new(StringComparer.Ordinal)
            {
                [LocalizedKeys.Auth.Unauthorized]          = "Unauthorized.",
                [LocalizedKeys.Auth.InvalidCredentials]    = "Invalid credentials.",
                [LocalizedKeys.Auth.SessionExpired]        = "Your session has expired. Please sign in again.",
                [LocalizedKeys.Auth.TokenInvalid]          = "The supplied token is invalid.",
                [LocalizedKeys.Auth.PasswordChangeFailed]  = "Password change failed.",
                [LocalizedKeys.Permissions.Forbidden]      = "You do not have permission to perform this action.",
                [LocalizedKeys.Permissions.ActionDescription.View]      = "Can view records",
                [LocalizedKeys.Permissions.ActionDescription.Insert]    = "Can add new records",
                [LocalizedKeys.Permissions.ActionDescription.EditClose] = "Can edit and close records",
                [LocalizedKeys.Permissions.ActionDescription.Open]      = "Can reopen records",
                [LocalizedKeys.Permissions.ActionDescription.Delete]    = "Can remove records",
                [LocalizedKeys.Permissions.ActionDescriptionFallback]   = "Can perform this action",
                [LocalizedKeys.Infrastructure.ValidationError]      = "Validation error.",
                [LocalizedKeys.Infrastructure.NotFound]             = "Resource not found.",
                [LocalizedKeys.Infrastructure.Conflict]             = "The request conflicts with the current state of the resource.",
                [LocalizedKeys.Infrastructure.ServerError]          = "An unexpected error occurred. Please try again later.",
                [LocalizedKeys.Infrastructure.DuplicateKey]         = "A record with the same unique value already exists.",
                [LocalizedKeys.Infrastructure.ForeignKeyViolation]  = "The request references a related record that does not exist.",
                [LocalizedKeys.Infrastructure.Required]             = "Field is required.",
                [LocalizedKeys.Infrastructure.Invalid]              = "Field value is invalid.",

                [LocalizedKeys.Courses.NotFound]                 = "Course not found.",
                [LocalizedKeys.Courses.CodeInUse]                = "Course code is already in use.",
                [LocalizedKeys.Courses.CreditHoursOutOfRange]    = "Credit hours must be between 0 and 12.",
                [LocalizedKeys.Courses.PlanNotFound]             = "Academic plan not found.",
                [LocalizedKeys.Courses.PlanCourseEntryNotFound]  = "Plan course entry not found.",
                [LocalizedKeys.Courses.PlanCourseAlreadyPresent] = "Course already present in this plan.",
                [LocalizedKeys.Courses.StructureNodeNotFound]    = "Structure node not found.",
                [LocalizedKeys.Courses.EffectiveToAfterFrom]     = "Effective-to must be after effective-from.",

                [LocalizedKeys.Semesters.NotFound]                 = "Semester not found.",
                [LocalizedKeys.Semesters.AcademicYearNotFound]     = "Academic year not found.",
                [LocalizedKeys.Semesters.AcademicYearMissing]      = "Academic year does not exist.",
                [LocalizedKeys.Semesters.DatesOverlap]             = "Semester dates overlap with an existing semester in the same academic year.",
                [LocalizedKeys.Semesters.DatesOutsideAcademicYear] = "Semester dates must be within the academic year range.",
                [LocalizedKeys.Semesters.EndAfterStart]            = "End date must be greater than start date.",
                [LocalizedKeys.Semesters.YearDatesOverlap]         = "Academic year dates overlap with an existing academic year.",

                [LocalizedKeys.Payments.InvoiceNotFound]    = "Invoice not found.",
                [LocalizedKeys.Payments.StudentNotFound]    = "Student not found.",
                [LocalizedKeys.Payments.AtLeastOneItem]     = "At least one invoice item is required.",
                [LocalizedKeys.Payments.PaidCannotCancel]   = "Paid invoices cannot be cancelled — issue a refund instead.",

                [LocalizedKeys.StudentInformation.ProfileRecordNotFound]    = "Profile record not found.",
                [LocalizedKeys.StudentInformation.StudentNotFound]          = "Student not found.",
                [LocalizedKeys.StudentInformation.InvalidJson]              = "DataJson must be a syntactically valid JSON document.",
                [LocalizedKeys.StudentInformation.CustomCategoryKeyRequired] = "CustomCategoryKey is required when Category is Custom.",

                [LocalizedKeys.CourseOfferings.NotFound]              = "Course offering not found.",
                [LocalizedKeys.CourseOfferings.SectionInUse]          = "A course offering with this section already exists for the same course, term, and structure node.",
                [LocalizedKeys.CourseOfferings.CapacityNegative]      = "Capacity must be non-negative.",
                [LocalizedKeys.CourseOfferings.CapacityBelowCount]    = "Capacity cannot be reduced below the current registration count.",
                [LocalizedKeys.CourseOfferings.SectionCodeRequired]   = "Section code is required.",
                [LocalizedKeys.CourseOfferings.IllegalStateTransition] = "The requested status or registration state is not allowed from the offering's current state.",

                [LocalizedKeys.Schedule.NotFound]       = "Schedule slot not found.",
                [LocalizedKeys.Schedule.DuplicateSlot]  = "A schedule slot for this offering already exists at the same day and time.",
                [LocalizedKeys.Schedule.EndBeforeStart] = "End time must be strictly greater than start time.",
                [LocalizedKeys.Schedule.SlotConflict]   = "The proposed time overlaps an existing schedule slot for this offering on the same day.",

                [LocalizedKeys.StudentServices.ServiceNotFound]              = "Student service not found.",
                [LocalizedKeys.StudentServices.ServiceInactive]              = "This service is currently inactive.",
                [LocalizedKeys.StudentServices.ServiceCodeInUse]             = "A service with this code already exists.",
                [LocalizedKeys.StudentServices.RequestNotFound]              = "Service request not found.",
                [LocalizedKeys.StudentServices.RequiredFieldMissing]         = "A required field is missing.",
                [LocalizedKeys.StudentServices.RequiredDocumentMissing]      = "A required document is missing.",
                [LocalizedKeys.StudentServices.InvalidFieldValue]            = "Field value is invalid for the configured type or constraints.",
                [LocalizedKeys.StudentServices.InvalidDropdownValue]         = "Selected value is not in the configured dropdown options.",
                [LocalizedKeys.StudentServices.InvalidFileExtension]         = "Uploaded file extension is not allowed for this document.",
                [LocalizedKeys.StudentServices.FileTooLarge]                 = "Uploaded file exceeds the configured maximum size.",
                [LocalizedKeys.StudentServices.InvalidTransition]            = "The requested workflow transition is not allowed from the current status.",
                [LocalizedKeys.StudentServices.UnauthorizedTransition]       = "You are not authorized to perform this workflow transition.",
                [LocalizedKeys.StudentServices.CannotCancelAfterProcessing]  = "The request cannot be cancelled once processing has begun.",
                [LocalizedKeys.StudentServices.WorkflowNotFound]             = "Workflow definition not found.",
                [LocalizedKeys.StudentServices.WorkflowCodeInUse]            = "A workflow with this code already exists.",
                [LocalizedKeys.StudentServices.WorkflowMissingInitial]       = "A workflow must declare exactly one initial state.",
                [LocalizedKeys.StudentServices.PaymentRequired]              = "Payment is required before the request can be processed.",
                [LocalizedKeys.StudentServices.RequestNotWaitingPayment]     = "The request is not in a waiting-for-payment state.",
                [LocalizedKeys.StudentServices.DuplicateFieldValue]          = "The same field was submitted more than once.",
            },
            ["ar"] = new(StringComparer.Ordinal)
            {
                [LocalizedKeys.Auth.Unauthorized]          = "غير مصرّح.",
                [LocalizedKeys.Auth.InvalidCredentials]    = "بيانات الاعتماد غير صحيحة.",
                [LocalizedKeys.Auth.SessionExpired]        = "انتهت صلاحية الجلسة. يُرجى تسجيل الدخول مرة أخرى.",
                [LocalizedKeys.Auth.TokenInvalid]          = "الرمز المُقدَّم غير صالح.",
                [LocalizedKeys.Auth.PasswordChangeFailed]  = "فشل تغيير كلمة المرور.",
                [LocalizedKeys.Permissions.Forbidden]      = "ليست لديك صلاحية لتنفيذ هذا الإجراء.",
                [LocalizedKeys.Permissions.ActionDescription.View]      = "إمكانية عرض السجلات",
                [LocalizedKeys.Permissions.ActionDescription.Insert]    = "إمكانية إضافة سجلات جديدة",
                [LocalizedKeys.Permissions.ActionDescription.EditClose] = "إمكانية تعديل وإغلاق السجلات",
                [LocalizedKeys.Permissions.ActionDescription.Open]      = "إمكانية إعادة فتح السجلات",
                [LocalizedKeys.Permissions.ActionDescription.Delete]    = "إمكانية حذف السجلات",
                [LocalizedKeys.Permissions.ActionDescriptionFallback]   = "إمكانية تنفيذ هذا الإجراء",
                [LocalizedKeys.Infrastructure.ValidationError]      = "خطأ في التحقق.",
                [LocalizedKeys.Infrastructure.NotFound]             = "المورد غير موجود.",
                [LocalizedKeys.Infrastructure.Conflict]             = "الطلب يتعارض مع الحالة الحالية للمورد.",
                [LocalizedKeys.Infrastructure.ServerError]          = "حدث خطأ غير متوقع. يُرجى المحاولة لاحقًا.",
                [LocalizedKeys.Infrastructure.DuplicateKey]         = "يوجد سجل آخر بنفس القيمة الفريدة.",
                [LocalizedKeys.Infrastructure.ForeignKeyViolation]  = "الطلب يُشير إلى سجلٍ مرتبط غير موجود.",
                [LocalizedKeys.Infrastructure.Required]             = "الحقل مطلوب.",
                [LocalizedKeys.Infrastructure.Invalid]              = "قيمة الحقل غير صالحة.",

                [LocalizedKeys.Courses.NotFound]                 = "المقرر غير موجود.",
                [LocalizedKeys.Courses.CodeInUse]                = "كود المقرر مستخدم بالفعل.",
                [LocalizedKeys.Courses.CreditHoursOutOfRange]    = "يجب أن تكون عدد الساعات المعتمدة بين 0 و 12.",
                [LocalizedKeys.Courses.PlanNotFound]             = "الخطة الدراسية غير موجودة.",
                [LocalizedKeys.Courses.PlanCourseEntryNotFound]  = "المقرر داخل الخطة غير موجود.",
                [LocalizedKeys.Courses.PlanCourseAlreadyPresent] = "هذا المقرر مُدرج بالفعل ضمن الخطة.",
                [LocalizedKeys.Courses.StructureNodeNotFound]    = "عقدة الهيكل غير موجودة.",
                [LocalizedKeys.Courses.EffectiveToAfterFrom]     = "يجب أن يكون تاريخ نهاية السريان بعد تاريخ بدايته.",

                [LocalizedKeys.Semesters.NotFound]                 = "الفصل الدراسي غير موجود.",
                [LocalizedKeys.Semesters.AcademicYearNotFound]     = "العام الأكاديمي غير موجود.",
                [LocalizedKeys.Semesters.AcademicYearMissing]      = "العام الأكاديمي غير موجود.",
                [LocalizedKeys.Semesters.DatesOverlap]             = "تواريخ الفصل تتداخل مع فصل آخر في نفس العام الأكاديمي.",
                [LocalizedKeys.Semesters.DatesOutsideAcademicYear] = "يجب أن تكون تواريخ الفصل ضمن نطاق العام الأكاديمي.",
                [LocalizedKeys.Semesters.EndAfterStart]            = "يجب أن يكون تاريخ النهاية بعد تاريخ البداية.",
                [LocalizedKeys.Semesters.YearDatesOverlap]         = "تواريخ العام الأكاديمي تتداخل مع عام أكاديمي آخر.",

                [LocalizedKeys.Payments.InvoiceNotFound]   = "الفاتورة غير موجودة.",
                [LocalizedKeys.Payments.StudentNotFound]   = "الطالب غير موجود.",
                [LocalizedKeys.Payments.AtLeastOneItem]    = "يجب توفير عنصر واحد على الأقل في الفاتورة.",
                [LocalizedKeys.Payments.PaidCannotCancel]  = "لا يمكن إلغاء فاتورة مدفوعة — يجب إصدار استرداد بدلاً من ذلك.",

                [LocalizedKeys.StudentInformation.ProfileRecordNotFound]    = "سجل الملف الشخصي غير موجود.",
                [LocalizedKeys.StudentInformation.StudentNotFound]          = "الطالب غير موجود.",
                [LocalizedKeys.StudentInformation.InvalidJson]              = "يجب أن تكون قيمة DataJson مستند JSON صالحًا.",
                [LocalizedKeys.StudentInformation.CustomCategoryKeyRequired] = "حقل CustomCategoryKey مطلوب عندما تكون الفئة Custom.",

                [LocalizedKeys.CourseOfferings.NotFound]              = "طرح المقرر غير موجود.",
                [LocalizedKeys.CourseOfferings.SectionInUse]          = "يوجد طرح آخر بنفس الشُعبة لنفس المقرر والفصل والعقدة التنظيمية.",
                [LocalizedKeys.CourseOfferings.CapacityNegative]      = "يجب أن تكون السعة قيمة غير سالبة.",
                [LocalizedKeys.CourseOfferings.CapacityBelowCount]    = "لا يمكن خفض السعة إلى أقل من عدد المسجلين الحاليين.",
                [LocalizedKeys.CourseOfferings.SectionCodeRequired]   = "كود الشُعبة مطلوب.",
                [LocalizedKeys.CourseOfferings.IllegalStateTransition] = "الحالة أو حالة التسجيل المطلوبة غير مسموح بها انطلاقًا من حالة الطرح الحالية.",

                [LocalizedKeys.Schedule.NotFound]       = "موعد الجدول الدراسي غير موجود.",
                [LocalizedKeys.Schedule.DuplicateSlot]  = "يوجد موعد آخر لنفس الطرح في نفس اليوم والتوقيت.",
                [LocalizedKeys.Schedule.EndBeforeStart] = "يجب أن يكون وقت النهاية بعد وقت البداية تمامًا.",
                [LocalizedKeys.Schedule.SlotConflict]   = "التوقيت المقترح يتعارض مع موعد آخر لنفس الطرح في نفس اليوم.",

                [LocalizedKeys.StudentServices.ServiceNotFound]              = "خدمة الطالب غير موجودة.",
                [LocalizedKeys.StudentServices.ServiceInactive]              = "هذه الخدمة غير مفعّلة حاليًا.",
                [LocalizedKeys.StudentServices.ServiceCodeInUse]             = "يوجد بالفعل خدمة بنفس الكود.",
                [LocalizedKeys.StudentServices.RequestNotFound]              = "طلب الخدمة غير موجود.",
                [LocalizedKeys.StudentServices.RequiredFieldMissing]         = "حقل مطلوب غير معبأ.",
                [LocalizedKeys.StudentServices.RequiredDocumentMissing]      = "مستند مطلوب غير مرفق.",
                [LocalizedKeys.StudentServices.InvalidFieldValue]            = "قيمة الحقل غير صالحة للنوع المُعد أو القيود المعرّفة.",
                [LocalizedKeys.StudentServices.InvalidDropdownValue]         = "القيمة المختارة ليست ضمن الخيارات المسموح بها.",
                [LocalizedKeys.StudentServices.InvalidFileExtension]         = "امتداد الملف المرفوع غير مسموح به لهذا المستند.",
                [LocalizedKeys.StudentServices.FileTooLarge]                 = "حجم الملف يتجاوز الحد الأقصى المسموح به.",
                [LocalizedKeys.StudentServices.InvalidTransition]            = "الانتقال المطلوب في سير العمل غير مسموح من الحالة الحالية.",
                [LocalizedKeys.StudentServices.UnauthorizedTransition]       = "ليست لديك صلاحية لتنفيذ هذا الانتقال في سير العمل.",
                [LocalizedKeys.StudentServices.CannotCancelAfterProcessing]  = "لا يمكن إلغاء الطلب بعد بدء معالجته.",
                [LocalizedKeys.StudentServices.WorkflowNotFound]             = "تعريف سير العمل غير موجود.",
                [LocalizedKeys.StudentServices.WorkflowCodeInUse]            = "يوجد بالفعل سير عمل بنفس الكود.",
                [LocalizedKeys.StudentServices.WorkflowMissingInitial]       = "يجب أن يحتوي سير العمل على حالة ابتدائية واحدة بالضبط.",
                [LocalizedKeys.StudentServices.PaymentRequired]              = "يجب إتمام الدفع قبل معالجة الطلب.",
                [LocalizedKeys.StudentServices.RequestNotWaitingPayment]     = "الطلب ليس في حالة انتظار الدفع.",
                [LocalizedKeys.StudentServices.DuplicateFieldValue]          = "تم إرسال نفس الحقل أكثر من مرة.",
            },
        };

    /// <summary>
    /// Returns the translation for <paramref name="key"/> in <paramref name="culture"/>,
    /// falling back to the default culture, then to <paramref name="key"/> itself.
    /// </summary>
    public static string Resolve(string key, string? culture)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        var lang = string.IsNullOrWhiteSpace(culture) ? DefaultCulture : culture!.ToLowerInvariant();

        if (Table.TryGetValue(lang, out var bucket) && bucket.TryGetValue(key, out var value))
            return value;

        if (Table.TryGetValue(DefaultCulture, out var defaultBucket) && defaultBucket.TryGetValue(key, out var defaultValue))
            return defaultValue;

        return key;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="key"/> is present in the
    /// translation table — used by GlobalExceptionHandler to decide whether
    /// to resolve a localization key vs. pass through a literal message.
    /// </summary>
    public static bool ContainsKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return Table.TryGetValue(DefaultCulture, out var bucket) && bucket.ContainsKey(key);
    }
}

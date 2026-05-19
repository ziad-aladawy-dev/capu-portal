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
                [LocalizedKeys.Infrastructure.ValidationError]      = "Validation error.",
                [LocalizedKeys.Infrastructure.NotFound]             = "Resource not found.",
                [LocalizedKeys.Infrastructure.Conflict]             = "The request conflicts with the current state of the resource.",
                [LocalizedKeys.Infrastructure.ServerError]          = "An unexpected error occurred. Please try again later.",
                [LocalizedKeys.Infrastructure.DuplicateKey]         = "A record with the same unique value already exists.",
                [LocalizedKeys.Infrastructure.ForeignKeyViolation]  = "The request references a related record that does not exist.",

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
            },
            ["ar"] = new(StringComparer.Ordinal)
            {
                [LocalizedKeys.Auth.Unauthorized]          = "غير مصرّح.",
                [LocalizedKeys.Auth.InvalidCredentials]    = "بيانات الاعتماد غير صحيحة.",
                [LocalizedKeys.Auth.SessionExpired]        = "انتهت صلاحية الجلسة. يُرجى تسجيل الدخول مرة أخرى.",
                [LocalizedKeys.Auth.TokenInvalid]          = "الرمز المُقدَّم غير صالح.",
                [LocalizedKeys.Auth.PasswordChangeFailed]  = "فشل تغيير كلمة المرور.",
                [LocalizedKeys.Permissions.Forbidden]      = "ليست لديك صلاحية لتنفيذ هذا الإجراء.",
                [LocalizedKeys.Infrastructure.ValidationError]      = "خطأ في التحقق.",
                [LocalizedKeys.Infrastructure.NotFound]             = "المورد غير موجود.",
                [LocalizedKeys.Infrastructure.Conflict]             = "الطلب يتعارض مع الحالة الحالية للمورد.",
                [LocalizedKeys.Infrastructure.ServerError]          = "حدث خطأ غير متوقع. يُرجى المحاولة لاحقًا.",
                [LocalizedKeys.Infrastructure.DuplicateKey]         = "يوجد سجل آخر بنفس القيمة الفريدة.",
                [LocalizedKeys.Infrastructure.ForeignKeyViolation]  = "الطلب يُشير إلى سجلٍ مرتبط غير موجود.",

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

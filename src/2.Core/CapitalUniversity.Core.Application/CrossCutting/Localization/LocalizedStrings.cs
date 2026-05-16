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
                [LocalizedKeys.Infrastructure.ValidationError] = "Validation error.",
                [LocalizedKeys.Infrastructure.NotFound]    = "Resource not found.",
                [LocalizedKeys.Infrastructure.Conflict]    = "The request conflicts with the current state of the resource.",
                [LocalizedKeys.Infrastructure.ServerError] = "An unexpected error occurred. Please try again later.",
            },
            ["ar"] = new(StringComparer.Ordinal)
            {
                [LocalizedKeys.Auth.Unauthorized]          = "غير مصرّح.",
                [LocalizedKeys.Auth.InvalidCredentials]    = "بيانات الاعتماد غير صحيحة.",
                [LocalizedKeys.Auth.SessionExpired]        = "انتهت صلاحية الجلسة. يُرجى تسجيل الدخول مرة أخرى.",
                [LocalizedKeys.Auth.TokenInvalid]          = "الرمز المُقدَّم غير صالح.",
                [LocalizedKeys.Auth.PasswordChangeFailed]  = "فشل تغيير كلمة المرور.",
                [LocalizedKeys.Permissions.Forbidden]      = "ليست لديك صلاحية لتنفيذ هذا الإجراء.",
                [LocalizedKeys.Infrastructure.ValidationError] = "خطأ في التحقق.",
                [LocalizedKeys.Infrastructure.NotFound]    = "المورد غير موجود.",
                [LocalizedKeys.Infrastructure.Conflict]    = "الطلب يتعارض مع الحالة الحالية للمورد.",
                [LocalizedKeys.Infrastructure.ServerError] = "حدث خطأ غير متوقع. يُرجى المحاولة لاحقًا.",
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
}

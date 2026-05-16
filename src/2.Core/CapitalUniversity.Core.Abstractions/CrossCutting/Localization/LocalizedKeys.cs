namespace CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

/// <summary>
/// Compile-time constants for every key the infrastructure-layer code passes to
/// <see cref="ILocalizationService.GetString"/>. Strings live in
/// <c>LocalizedStrings</c> per culture; the indirection keeps controllers and
/// middleware out of the raw literal business.
///
/// <para>
/// Naming: <c>{Area}.{Reason}</c>. Areas are limited to cross-cutting infrastructure
/// (Auth, Permissions, Infrastructure validation) — teammate business messages stay
/// out of this catalogue per the scoping rules.
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
    }
}

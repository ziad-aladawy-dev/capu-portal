namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

/// <summary>
/// Custom JWT claim names that the auth pipeline uses for stateless revocation.
/// </summary>
public static class SessionClaims
{
    /// <summary>
    /// Integer snapshot of the user's <c>SessionVersion</c> at token issue time.
    /// The middleware compares this to the current row value on every authenticated
    /// request; a mismatch invalidates the bearer.
    /// </summary>
    public const string SessionVersion = "session_version";
}

namespace CapitalUniversity.Core.Abstractions.Shared.BulkActions;

/// <summary>
/// A single per-row failure inside a <see cref="BulkActionResult"/>. <see cref="Code"/>
/// is the canonical machine-readable failure category (matches
/// <see cref="BulkFailureCodes"/>); <see cref="Message"/> is the localized
/// human-facing detail.
/// </summary>
public sealed class BulkActionFailure
{
    public Guid Id { get; init; }
    public string Code { get; init; } = BulkFailureCodes.Unknown;
    public string Message { get; init; } = string.Empty;
}

/// <summary>Canonical codes used in <see cref="BulkActionFailure.Code"/>.</summary>
public static class BulkFailureCodes
{
    public const string NotFound = "NotFound";
    public const string Forbidden = "Forbidden";
    public const string InvalidTransition = "InvalidTransition";
    public const string Conflict = "Conflict";
    public const string Validation = "Validation";
    public const string Unknown = "Unknown";
}

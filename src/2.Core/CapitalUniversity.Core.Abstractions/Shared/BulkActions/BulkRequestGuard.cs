using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common.Exceptions;

namespace CapitalUniversity.Core.Abstractions.Shared.BulkActions;

/// <summary>
/// Controller-level guards for the bulk-action envelopes. Throws the project's
/// domain <see cref="ValidationException"/> with a <see cref="LocalizedKeys"/>
/// catalog key — never a literal English string — so the global exception
/// handler maps the failure to a localized 400 ProblemDetails with the
/// standard <c>errors</c> dictionary on the way out.
///
/// <para>
/// Use these in every bulk endpoint instead of inline <c>BadRequest(new {
/// Message = "..." })</c> checks; that pattern bypasses localization and the
/// uniform error shape.
/// </para>
/// </summary>
public static class BulkRequestGuard
{
    /// <summary>
    /// Asserts that the caller supplied a non-empty id list under the
    /// <see cref="BulkConstants.MaxBulkSize"/> cap. The <paramref name="propertyName"/>
    /// is the wire-shape field the failure attaches to (typically <c>"ids"</c>).
    /// </summary>
    public static void EnsureValidIds(IReadOnlyList<Guid>? ids, string propertyName = "ids")
    {
        if (ids is null || ids.Count == 0)
        {
            throw new ValidationException(propertyName, LocalizedKeys.BulkActions.IdsRequired);
        }
        if (ids.Count > BulkConstants.MaxBulkSize)
        {
            throw new ValidationException(propertyName, LocalizedKeys.BulkActions.BatchExceedsMaxSize);
        }
    }

    /// <summary>
    /// Asserts that the caller supplied a non-empty record list under the cap.
    /// Same shape as <see cref="EnsureValidIds"/> but the localized message is
    /// "records required" — fits batch-upsert endpoints where the payload is
    /// objects, not ids.
    /// </summary>
    public static void EnsureRecords<T>(IReadOnlyList<T>? records, string propertyName = "records")
    {
        if (records is null || records.Count == 0)
        {
            throw new ValidationException(propertyName, LocalizedKeys.BulkActions.RecordsRequired);
        }
        if (records.Count > BulkConstants.MaxBulkSize)
        {
            throw new ValidationException(propertyName, LocalizedKeys.BulkActions.BatchExceedsMaxSize);
        }
    }

    /// <summary>
    /// Asserts that the request's <c>payload</c> object is present.
    /// </summary>
    public static void EnsurePayload(object? payload, string propertyName = "payload")
    {
        if (payload is null)
        {
            throw new ValidationException(propertyName, LocalizedKeys.BulkActions.PayloadRequired);
        }
    }

    /// <summary>
    /// Asserts that a required free-text reason is non-blank.
    /// </summary>
    public static void EnsureReason(string? reason, string propertyName = "reason")
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException(propertyName, LocalizedKeys.BulkActions.ReasonRequired);
        }
    }

    /// <summary>
    /// Asserts that the verifier id is supplied and non-empty.
    /// </summary>
    public static void EnsureVerifiedBy(Guid verifiedBy, string propertyName = "verifiedBy")
    {
        if (verifiedBy == Guid.Empty)
        {
            throw new ValidationException(propertyName, LocalizedKeys.BulkActions.VerifiedByRequired);
        }
    }
}

namespace CapitalUniversity.Core.Abstractions.Shared.BulkActions;

/// <summary>
/// Outcome of a bulk operation. Partial failure is normal: <see cref="SucceededIds"/>
/// and <see cref="Failures"/> together cover every id from the request. The HTTP
/// status code stays 200 — clients inspect this payload to decide what to do
/// with the failures.
/// </summary>
public sealed class BulkActionResult
{
    public int Succeeded => SucceededIds.Count;

    public int Failed => Failures.Count;

    public IReadOnlyList<Guid> SucceededIds { get; init; } = Array.Empty<Guid>();

    public IReadOnlyList<BulkActionFailure> Failures { get; init; } = Array.Empty<BulkActionFailure>();

    public static BulkActionResult From(
        IEnumerable<Guid> succeededIds,
        IEnumerable<BulkActionFailure> failures) => new()
    {
        SucceededIds = succeededIds.ToList(),
        Failures = failures.ToList(),
    };
}

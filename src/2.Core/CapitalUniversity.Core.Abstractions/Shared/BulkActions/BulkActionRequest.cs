namespace CapitalUniversity.Core.Abstractions.Shared.BulkActions;

/// <summary>
/// Standard "act on a list of ids" envelope for Phase 1 bulk endpoints.
/// <typeparamref name="TPayload"/> carries any operation-specific parameters
/// (target status, reason, etc.) and may be omitted (use <see cref="BulkActionRequest"/>)
/// when the action is parameterless (e.g. mark-notifications-read).
/// </summary>
/// <remarks>
/// Wire shape:
/// <code>
/// { "ids": ["..."], "payload": { ...operation-specific... } }
/// </code>
/// </remarks>
public class BulkActionRequest<TPayload>
{
    public IReadOnlyList<Guid> Ids { get; init; } = Array.Empty<Guid>();

    public TPayload? Payload { get; init; }
}

/// <summary>Parameterless variant — only the id list is needed.</summary>
public class BulkActionRequest
{
    public IReadOnlyList<Guid> Ids { get; init; } = Array.Empty<Guid>();
}

namespace CapitalUniversity.Sync.Abstractions.Contracts;

/// <summary>
/// Optional opt-in surface for extractors that track an advancing cursor during
/// extraction so the module can persist it to <c>SyncCheckpoint</c> afterwards.
///
/// <para>
/// Before this interface, modules read the cursor by name from each extractor's
/// concrete type (e.g. <c>StudentExtractor.MaxExternalUpdatedAt</c>), forcing a
/// compile-time coupling between module and concrete extractor. Any test that
/// mocked <see cref="IDataExtractor{TExternal}"/> silently disabled checkpoint
/// advancement — the cursor property doesn't exist on the interface so the cast
/// failed and the module skipped the save.
/// </para>
///
/// <para>
/// Extractors that implement this expose the cursor as a serialized string so
/// the module can persist it to <see cref="Models.SyncCheckpoint.Cursor"/>
/// without knowing the cursor's underlying type. Extractors choose the encoding
/// (ISO-8601 timestamp, integer-as-string, opaque token, etc.); modules treat
/// it as an opaque value.
/// </para>
/// </summary>
public interface ICursorObserver
{
    /// <summary>
    /// Most-advanced cursor value observed during the most recent
    /// <see cref="IDataExtractor{TExternal}.ExtractAsync"/> enumeration.
    /// <c>null</c> when no records were yielded.
    /// </summary>
    string? CurrentCursor { get; }
}
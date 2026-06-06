namespace CapitalUniversity.Sync.Abstractions.Contracts;

/// <summary>
/// Optional per-record validation. Invalid records are dropped from the batch and
/// counted as failures; the pipeline does not throw.
///
/// <para>
/// <b>Required: emit normalized category strings, not record-specific data.</b>
/// </para>
/// <para>
/// The pipeline aggregates validation errors into a <c>Dictionary&lt;string,int&gt;</c>
/// keyed by message. If validators embed per-record identifiers, timestamps, or values
/// in their error strings, that dictionary grows linearly with invalid records and can
/// dominate worker memory on large syncs. The pipeline applies a hard cap
/// (<c>SyncPipeline.MaxDistinctWarnings</c>) as defense-in-depth, but validators MUST
/// cooperate.
/// </para>
///
/// <para>GOOD:</para>
/// <code>
/// error = "Email format invalid.";
/// error = "ExternalStudentId is required.";
/// </code>
///
/// <para>BAD (causes unbounded growth):</para>
/// <code>
/// error = $"Student {id} has invalid email '{email}' at {DateTime.UtcNow:O}";
/// </code>
/// </summary>
public interface IRecordValidator<in TRecord>
{
    bool IsValid(TRecord record, out string? error);
}
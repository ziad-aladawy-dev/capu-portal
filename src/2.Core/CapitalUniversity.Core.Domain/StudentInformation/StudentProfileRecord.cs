using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.StudentInformation;

/// <summary>
/// Sparse, optional, regulation-driven facts about a single student. Stored
/// as JSON to keep the schema flexible — Student Information must NOT model
/// each category as its own SQL table, and must NOT use key-value tables
/// (per <c>docs/Plan.md</c> Student Information Rules).
///
/// <para>
/// <see cref="SchemaVersion"/> is per-category. Consumers parse
/// <see cref="DataJson"/> against the version they understand — the
/// <c>StudentInformation</c> module never interprets the payload.
/// </para>
///
/// <para>
/// <see cref="IsSensitive"/> drives audit logging + cache TTL caps — set
/// for medical / disability / military records.
/// </para>
/// </summary>
public class StudentProfileRecord : BaseEntity
{
    /// <summary>Cross-module reference to the owning student. No EF navigation — modularity rule.</summary>
    public Guid StudentId { get; set; }

    public StudentProfileCategory Category { get; set; } = StudentProfileCategory.Custom;

    /// <summary>Free-text identifier when <see cref="Category"/> is <c>Custom</c>; ignored otherwise.</summary>
    public string CustomCategoryKey { get; set; } = string.Empty;

    /// <summary>Per-category schema version. Producer-defined.</summary>
    public int SchemaVersion { get; set; }

    /// <summary>Serialised payload. nvarchar(max). Never EF-bound; treated as opaque blob.</summary>
    public string DataJson { get; set; } = "{}";

    /// <summary>Staff ID that verified the data, if applicable.</summary>
    public Guid? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    /// <summary>True for medical / disability / military / similar restricted categories. Drives audit logging.</summary>
    public bool IsSensitive { get; set; }
}

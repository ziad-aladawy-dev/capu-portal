using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation;

namespace CapitalUniversity.Modules.Student.Domain;

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
public class StudentProfileRecord : BaseEntity, ISoftDeletable
{
    /// <summary>Cross-module reference to the owning student. No EF navigation — modularity rule.</summary>
    public Guid StudentId { get; set; }

    /// <summary>SQL Server <c>rowversion</c>. EF maps this to optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

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

    /// <summary>
    /// Closable lifecycle. Once true the entity is immutable under
    /// <see cref="EnsureMutable"/>. Only callers holding the <c>Open</c>
    /// permission may reopen via <see cref="Reopen"/>.
    /// </summary>
    public bool IsClosed { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    /// <summary>
    /// Guards every mutating operation on the entity.
    /// </summary>
    public void EnsureMutable()
    {
        if (IsClosed)
            throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student profile record is closed and cannot be modified. Reopen it first.");
    }

    public void Close()
    {
        if (IsClosed) throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student profile record is already closed.");
        IsClosed = true;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (!IsClosed) throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student profile record is not closed.");
        IsClosed = false;
        ClosedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}

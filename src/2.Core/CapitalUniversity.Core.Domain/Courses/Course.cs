using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Courses;

/// <summary>
/// Catalog course entity — the stable, school-wide definition of a deliverable
/// unit of study. Per the platform plan: Courses module owns the catalog only;
/// prerequisites, enrollment, transcript, and GPA logic belong to a future
/// Registration module and MUST NOT live on this entity.
/// </summary>
public class Course : BaseEntity, IExternallySourced
{
    /// <summary>
    /// Composed sync-provenance block. <see cref="BaseEntity"/> inheritance is
    /// untouched; this property carries the upstream-merge-key + external-wins
    /// fields the sync write gateway needs.
    /// </summary>
    public ExternallySourcedData ExternallySourced { get; set; } = new();

    private string _code = string.Empty;

    /// <summary>Unique short code (e.g. <c>"CS101"</c>). Trimmed and upper-cased on set so the catalog cannot drift on whitespace or casing.</summary>
    public string Code
    {
        get => _code;
        set => _code = (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    /// <summary>Human-readable title (e.g. <c>"Introduction to Algorithms"</c>).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Credit hours awarded on successful completion. Non-negative.</summary>
    public int CreditHours { get; set; }

    /// <summary>Category — see <see cref="CourseCategory"/>.</summary>
    public CourseCategory Category { get; set; } = CourseCategory.Unspecified;

    /// <summary>Soft-disable flag. Inactive courses stay in the catalog for history but no longer surface in active plans.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Closable lifecycle. Once true the entity is immutable under
    /// <see cref="EnsureMutable"/>. Only callers holding the <c>Open</c>
    /// permission may reopen via <see cref="Reopen"/>.
    /// </summary>
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }

    public void EnsureMutable()
    {
        if (IsClosed)
            throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Course is closed and cannot be modified. Reopen it first.");
    }

    public void Close()
    {
        if (IsClosed) throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Course is already closed.");
        IsClosed = true;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (!IsClosed) throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Course is not closed.");
        IsClosed = false;
        ClosedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}

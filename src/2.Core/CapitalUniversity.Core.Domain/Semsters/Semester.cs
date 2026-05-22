using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Common.Exceptions;

namespace CapitalUniversity.Core.Domain.Semsters;

public class Semester : BaseEntity
{
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Closable lifecycle. Once true the entity is immutable under
    /// <see cref="EnsureMutable"/>. Only callers holding the <c>Open</c>
    /// permission may reopen via <see cref="Reopen"/>.
    /// </summary>
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }

    public AcademicYear AcademicYear { get; set; } = null!;

    public void EnsureMutable()
    {
        if (IsClosed)
            throw new ConflictException("Semester is closed and cannot be modified. Reopen it first.");
    }

    public void Close()
    {
        if (IsClosed) throw new ConflictException("Semester is already closed.");
        IsClosed = true;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (!IsClosed) throw new ConflictException("Semester is not closed.");
        IsClosed = false;
        ClosedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}

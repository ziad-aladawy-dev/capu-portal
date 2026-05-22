using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Common.Exceptions;

namespace CapitalUniversity.Core.Domain.Semsters;

public class AcademicYear : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Closable lifecycle. Once true the entity is immutable under
    /// <see cref="EnsureMutable"/> — only callers holding the <c>Open</c>
    /// permission can flip it back via <see cref="Reopen"/>.
    /// </summary>
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }

    public ICollection<Semester> Semesters { get; set; } = new List<Semester>();

    /// <summary>
    /// Guards every mutating operation on the entity. Repositories alone are
    /// not sufficient — call this from the domain service before applying any
    /// update so closure is enforced consistently across code paths.
    /// </summary>
    public void EnsureMutable()
    {
        if (IsClosed)
            throw new ConflictException("Academic year is closed and cannot be modified. Reopen it first.");
    }

    public void Close()
    {
        if (IsClosed) throw new ConflictException("Academic year is already closed.");
        IsClosed = true;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (!IsClosed) throw new ConflictException("Academic year is not closed.");
        IsClosed = false;
        ClosedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}

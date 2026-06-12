using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.UniversityStructure;

namespace CapitalUniversity.Core.Domain.Identity;

public class Student : BaseEntity, IExternallySourced
{
    public string StudentCode { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public DateTime BirthDate { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public string? Gender { get; set; }

    public string? GuardianName { get; set; }

    public string? GuardianPhone { get; set; }

    public Guid StructureNodeId { get; set; }

    public StructureNode StructureNode { get; set; } = null!;

    public DateTime? PasswordExpiry { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// See Staff.SessionVersion.
    /// </summary>
    public int SessionVersion { get; set; }

    /// <summary>
    /// Composed sync-provenance block. <see cref="BaseEntity"/> inheritance is
    /// untouched; this property carries the upstream-merge-key + external-wins
    /// fields the sync write gateway needs. Always non-null — Core-created
    /// rows have <see cref="ExternallySourcedData.ExternalId"/> = <c>null</c>.
    /// </summary>
    public ExternallySourcedData ExternallySourced { get; set; } = new();

    public bool IsClosed { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    public void EnsureMutable()
    {
        if (IsClosed)
            throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student record is closed and cannot be modified.");
    }

    public void Close()
    {
        if (IsClosed) throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student record is already closed.");
        IsClosed = true;
        ClosedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (!IsClosed) throw new CapitalUniversity.Core.Domain.Common.Exceptions.ConflictException("Student record is not closed.");
        IsClosed = false;
        ClosedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}

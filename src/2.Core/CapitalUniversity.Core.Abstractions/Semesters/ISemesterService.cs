using CapitalUniversity.Core.Abstractions.Semesters.DTOs;

namespace CapitalUniversity.Core.Abstractions.Semesters;

public interface ISemesterService
{
    Task<SemesterResponse?> GetByIdAsync(Guid id);
    Task<SemesterResponse?> GetCurrentAsync();
    Task<IEnumerable<SemesterResponse>> GetByAcademicYearIdAsync(Guid academicYearId);
    Task<Guid> CreateAsync(CreateSemesterRequest request);
    Task UpdateAsync(Guid id, UpdateSemesterRequest request);
    Task DeleteAsync(Guid id);
    /// <summary>
    /// Recomputes the <c>IsCurrent</c> flag across all semesters in the
    /// current academic year against the server's UTC clock and persists
    /// changes. WRITE operation: UPDATEs the <c>IsCurrent</c> column on zero
    /// or more rows and <c>UpdatedAt</c> on every mutated row. When no
    /// academic year is current, all semester flags are cleared. Idempotent
    /// — invoking twice with no intervening date change is a no-op. Uses a
    /// two-phase commit to satisfy the filtered UNIQUE index on
    /// <c>(AcademicYearId, IsCurrent) WHERE IsCurrent = 1</c>.
    /// </summary>
    Task ResolveCurrentSemesterAsync();
    Task CloseRecordAsync(Guid id);
    Task OpenRecordAsync(Guid id);
}

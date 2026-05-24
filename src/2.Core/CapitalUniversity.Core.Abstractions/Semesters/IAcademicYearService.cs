using CapitalUniversity.Core.Abstractions.Semesters.DTOs;

namespace CapitalUniversity.Core.Abstractions.Semesters;

public interface IAcademicYearService
{
    Task<AcademicYearResponse?> GetByIdAsync(Guid id);
    Task<AcademicYearResponse?> GetCurrentAsync();
    Task<IEnumerable<AcademicYearResponse>> GetAllAsync();
    Task<Guid> CreateAsync(CreateAcademicYearRequest request);
    Task UpdateAsync(Guid id, UpdateAcademicYearRequest request);
    Task DeleteAsync(Guid id);
    /// <summary>
    /// Recomputes the <c>IsCurrent</c> flag across all academic years against
    /// the server's UTC clock and persists changes. WRITE operation: UPDATEs
    /// the <c>IsCurrent</c> column on zero or more rows and <c>UpdatedAt</c> on
    /// every mutated row. Idempotent — invoking twice with no intervening date
    /// change is a no-op. Uses a two-phase commit to satisfy the filtered
    /// UNIQUE index on <c>(IsCurrent WHERE IsCurrent = 1)</c>.
    /// </summary>
    Task ResolveCurrentYearAsync();
    Task CloseRecordAsync(Guid id);
    Task OpenRecordAsync(Guid id);
}

using CapitalUniversity.Core.Domain.Semsters;

namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface IAcademicYearRepository
{
    Task<AcademicYear?> GetByIdAsync(Guid id);
    Task<AcademicYear?> GetCurrentAsync();
    Task<IReadOnlyList<AcademicYear>> GetAllAsync();
    Task<bool> HasOverlapAsync(DateTime startDate, DateTime endDate, Guid? excludeId = null);
    Task AddAsync(AcademicYear academicYear);
    void Update(AcademicYear academicYear);
    void Delete(AcademicYear academicYear);
}

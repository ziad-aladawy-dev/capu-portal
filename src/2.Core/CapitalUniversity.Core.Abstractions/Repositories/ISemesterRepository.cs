using CapitalUniversity.Core.Domain.Semsters;

namespace CapitalUniversity.Core.Domain.Repositories;

public interface ISemesterRepository
{
    Task<Semester?> GetByIdAsync(Guid id);
    Task<Semester?> GetCurrentAsync();
    Task<IEnumerable<Semester>> GetByAcademicYearIdAsync(Guid academicYearId);
    Task<bool> HasOverlapAsync(Guid academicYearId, DateTime startDate, DateTime endDate, Guid? excludeId = null);
    Task AddAsync(Semester semester);
    void Update(Semester semester);
    void Delete(Semester semester);
}

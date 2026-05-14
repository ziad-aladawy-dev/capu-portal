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
    Task ResolveCurrentSemesterAsync();
}

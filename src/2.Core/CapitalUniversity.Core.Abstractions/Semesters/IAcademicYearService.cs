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
    Task ResolveCurrentYearAsync();
}

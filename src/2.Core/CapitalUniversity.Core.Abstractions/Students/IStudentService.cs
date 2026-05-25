using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Core.Abstractions.Students.DTOs;

namespace CapitalUniversity.Core.Abstractions.Students;

public interface IStudentService
{
    Task<Guid> CreateAsync(CreateStudentRequest request);

    Task UpdateAsync(Guid id, UpdateStudentRequest request);

    Task DeleteAsync(Guid id);

    Task ToggleStatusAsync(Guid id);

    Task<StudentDto?> GetByIdAsync(Guid id);

    Task<List<StudentDto>> GetAllAsync();

    Task<PagedResult<StudentDto>> SearchAsync(StudentQueryRequest request);

    Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request);
}
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Students.DTOs;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(Guid id);

    Task<List<Student>> GetAllAsync();

    Task<PagedResult<Student>> SearchAsync(StudentQueryRequest request);

    Task AddAsync(Student student);

    Task UpdateAsync(Student student);

    Task SoftDeleteAsync(Guid id);

    Task<bool> ExistsAsync(Guid id);

    Task<bool> StudentCodeExistsAsync(string studentCode);

    Task<bool> EmailExistsAsync(string email);

    Task<bool> NationalIdExistsAsync(string nationalId);

    Task<string?> GetLastStudentCodeAsync();

    Task ToggleStatusAsync(Guid id);

    Task SaveChangesAsync();
}
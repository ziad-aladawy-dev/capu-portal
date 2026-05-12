using CapitalUniversity.Core.Abstractions.Students.DTOs;

namespace CapitalUniversity.Core.Abstractions.Students;

public interface IStudentService
{
    Task<Guid> CreateAsync(CreateStudentRequest request);

    Task UpdateAsync(Guid id, UpdateStudentRequest request);

    Task DeleteAsync(Guid id);

    Task<StudentDto?> GetByIdAsync(Guid id);

    Task<List<StudentDto>> GetAllAsync();
}
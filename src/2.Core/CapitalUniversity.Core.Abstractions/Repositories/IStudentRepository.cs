using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Domain.Repositories;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(Guid id);

    Task<List<Student>> GetAllAsync();

    Task AddAsync(Student student);

    Task UpdateAsync(Student student);

    Task SoftDeleteAsync(Guid id);

    Task<bool> ExistsAsync(Guid id);

    Task<bool> StudentCodeExistsAsync(string studentCode);

    Task SaveChangesAsync();
}

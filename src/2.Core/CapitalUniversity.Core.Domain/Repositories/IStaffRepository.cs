using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface IStaffRepository
{
    Task<Staff?> GetByIdAsync(Guid id);

    Task<List<Staff>> GetAllAsync();

    Task AddAsync(Staff staff);

    Task UpdateAsync(Staff staff);

    Task SoftDeleteAsync(Guid id);

    Task<bool> ExistsAsync(Guid id);

    Task<bool> EmployeeCodeExistsAsync(string employeeCode);

    Task SaveChangesAsync();
}
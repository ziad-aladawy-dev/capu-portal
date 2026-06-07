using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface IStaffRepository
{
    Task<Staff?> GetByIdAsync(Guid id);

    Task<List<Staff>> GetAllAsync();

    Task<PagedResult<Staff>> SearchAsync(StaffQueryRequest request);

    Task AddAsync(Staff staff);

    Task UpdateAsync(Staff staff);

    Task SoftDeleteAsync(Guid id);

    Task<bool> ExistsAsync(Guid id);

    Task<bool> EmployeeCodeExistsAsync(string employeeCode);

    Task<bool> EmailExistsAsync(string email);

    Task<bool> NationalIdExistsAsync(string nationalId);

    Task<string?> GetLastEmployeeCodeAsync();

    Task ToggleStatusAsync(Guid id);

    Task SaveChangesAsync();

    Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request);
}
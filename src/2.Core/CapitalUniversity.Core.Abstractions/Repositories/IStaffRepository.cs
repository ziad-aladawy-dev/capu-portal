using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;
using CapitalUniversity.Core.Domain.Identity;

namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface IStaffRepository
{
    Task<Staff?> GetByIdAsync(Guid id);

    /// <summary>Batch lookup by IDs. Single query. No N+1.</summary>
    Task<IReadOnlyList<Staff>> GetRangeAsync(IReadOnlyList<Guid> ids);

    Task<List<Staff>> GetAllAsync();

    Task<PagedResult<Staff>> SearchAsync(StaffQueryRequest request);

    Task AddAsync(Staff staff);

    Task AddRangeAsync(IReadOnlyList<Staff> staffList);

    Task UpdateAsync(Staff staff);

    Task SoftDeleteAsync(Guid id);

    Task<bool> ExistsAsync(Guid id);

    Task<bool> EmployeeCodeExistsAsync(string employeeCode);

    Task<bool> EmailExistsAsync(string email);

    Task<bool> NationalIdExistsAsync(string nationalId);

    Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request);

    Task<string?> GetLastEmployeeCodeAsync();

    Task ToggleStatusAsync(Guid id);

    Task SaveChangesAsync();
}
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;

namespace CapitalUniversity.Core.Abstractions.StaffManagement;

public interface IStaffService
{
    Task<Guid> CreateAsync(CreateStaffRequest request);

    Task<List<Guid>> BulkCreateAsync(IReadOnlyList<CreateStaffRequest> requests);

    Task UpdateAsync(Guid id, UpdateStaffRequest request);

    Task DeleteAsync(Guid id);

    Task ToggleStatusAsync(Guid id);

    Task<StaffDto?> GetByIdAsync(Guid id);

    /// <summary>Batch lookup by IDs. Single query. No N+1.</summary>
    Task<IReadOnlyList<StaffDto>> GetRangeAsync(IReadOnlyList<Guid> ids);

    Task<List<StaffDto>> GetAllAsync();

    Task<PagedResult<StaffDto>> SearchAsync(StaffQueryRequest request);

    Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request);

    Task UpdatePhotoAsync(Guid id, string photoUrl);
}
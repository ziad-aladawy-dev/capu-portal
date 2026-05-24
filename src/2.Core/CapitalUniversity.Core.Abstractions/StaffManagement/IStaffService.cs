using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Core.Abstractions.StaffManagement.DTOs;

namespace CapitalUniversity.Core.Abstractions.StaffManagement;

public interface IStaffService
{
    Task<Guid> CreateAsync(CreateStaffRequest request);

    Task UpdateAsync(Guid id, UpdateStaffRequest request);

    Task DeleteAsync(Guid id);

    Task ToggleStatusAsync(Guid id);

    Task<StaffDto?> GetByIdAsync(Guid id);

    Task<List<StaffDto>> GetAllAsync();

    Task<PagedResult<StaffDto>> SearchAsync(StaffQueryRequest request);

    Task<UserStatisticsDto> GetStatisticsAsync(UserStatisticsRequest request);

    /// <summary>3.8 — bulk set status (idempotent, explicit value).</summary>
    Task<BulkActionResult> SetStatusManyAsync(IReadOnlyList<Guid> ids, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>3.9 — bulk soft-delete.</summary>
    Task<BulkActionResult> DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
}
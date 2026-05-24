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

    /// <summary>
    /// 3.7 — bulk set status. Idempotent at the batch level — explicit value
    /// (not a flip) so replays land on the same state.
    /// </summary>
    Task<BulkActionResult> SetStatusManyAsync(IReadOnlyList<Guid> ids, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// 3.9 — bulk soft-delete. Per-row commits; failures land in the result map.
    /// </summary>
    Task<BulkActionResult> DeleteManyAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
}
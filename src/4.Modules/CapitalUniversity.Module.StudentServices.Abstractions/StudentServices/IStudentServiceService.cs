using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;

namespace CapitalUniversity.Modules.StudentServices.Abstractions;

/// <summary>
/// Owns the service-catalog write/read side. The student-facing
/// <c>GetAvailableAsync</c> returns only enabled, non-deleted services; the
/// admin-facing <c>GetAllAsync</c> + <c>GetByIdAsync</c> surface the full set
/// for staff who hold the <c>student-services.services.View</c> permission.
/// </summary>
public interface IStudentServiceService
{
    Task<StudentServiceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResponse<StudentServiceSummaryResponse>> GetAllAsync(
        int page,
        int pageSize,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentServiceSummaryResponse>> GetAvailableAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateStudentServiceRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateStudentServiceRequest request, CancellationToken cancellationToken = default);

    Task ToggleStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

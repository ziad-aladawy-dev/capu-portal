using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Services;

public interface IStudentRequestService
{
    Task<StudentRequestDto> GetRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<List<StudentRequestDto>> GetStudentRequestsAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<StudentRequestDto> CreateDraftAsync(Guid studentId, Guid serviceId, CancellationToken cancellationToken = default);
    Task<StudentRequestDto> SaveStepDataAsync(Guid requestId, string stepKey, object data, CancellationToken cancellationToken = default);
    Task<StudentRequestDto> SubmitRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StudentRequestDto> AssignToStaffAsync(Guid requestId, Guid staffId, CancellationToken cancellationToken = default);
    Task<StudentRequestDto> UpdateStatusAsync(Guid requestId, RequestStatus newStatus, string? comment = null, CancellationToken cancellationToken = default);
    Task<StudentRequestDto> AddCommentAsync(Guid requestId, string comment, Guid? performedByUserId, string role, CancellationToken cancellationToken = default);
    Task<List<StudentRequestDto>> GetPendingAssignmentsAsync(Guid staffId, CancellationToken cancellationToken = default);
}
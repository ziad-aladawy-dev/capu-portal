using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.Hubs;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace CapitalUniversity.Module.StudentServices.Application;

public class StudentRequestService : IStudentRequestService
{
    private readonly IStudentRequestRepository _requestRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly INotificationService _notificationService;
    private readonly IHubContext<StudentServicesHub> _hubContext;
    private readonly ICurrentUser _currentUser;
    private readonly IEffectiveScope _effectiveScope;

    public StudentRequestService(
        IStudentRequestRepository requestRepository,
        IServiceRepository serviceRepository,
        INotificationService notificationService,
        IHubContext<StudentServicesHub> hubContext,
        ICurrentUser currentUser,
        IEffectiveScope effectiveScope)
    {
        _requestRepository = requestRepository;
        _serviceRepository = serviceRepository;
        _notificationService = notificationService;
        _hubContext = hubContext;
        _currentUser = currentUser;
        _effectiveScope = effectiveScope;
    }

    public async Task<StudentRequestDto> GetStudentRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        return MapToDto(request);
    }

    public async Task<List<StudentRequestDto>> GetStudentRequestsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(studentId, cancellationToken);

        var requests = await _requestRepository.GetByStudentIdAsync(studentId, cancellationToken);
        return requests.Select(MapToDto).ToList();
    }

    public async Task<List<StaffRequestListItemDto>> GetAllRequestsForStaffAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.GetAllForStaffAsync(cancellationToken);
        
        // Apply scope filtering in-memory
        var filteredRequests = new List<StaffRequestListItemDto>();
        foreach (var req in requests)
        {
            if (await _effectiveScope.CanAccessStudentAsync(req.StudentId, cancellationToken))
            {
                filteredRequests.Add(req);
            }
        }

        return filteredRequests;
    }

    public async Task<StudentRequestDto> CreateDraftAsync(Guid studentId, Guid serviceId, CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(studentId, cancellationToken);

        var service = await _serviceRepository.GetByIdWithWorkflowAsync(serviceId, cancellationToken);
        if (service == null || !service.IsActive) throw new NotFoundException("Service not found or inactive");

        var request = new StudentRequest
        {
            StudentId = studentId,
            ServiceId = serviceId,
            Status = RequestStatus.Draft,
            PaymentStatus = service.IsPaid ? PaymentStatus.Pending : PaymentStatus.NotRequired,
            CurrentStepOrder = 0
        };

        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "Created",
            Comment = "Request draft created",
            PerformedByUserId = studentId,
            PerformedByRole = "Student",
            PerformedAt = DateTime.UtcNow
        });

        await _requestRepository.AddAsync(request, cancellationToken);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        return await GetStudentRequestAsync(request.Id, cancellationToken);
    }

    public async Task<StudentRequestDto> SaveStepDataAsync(Guid requestId, string stepKey, object data, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        if (request.Status != RequestStatus.Draft)
            throw new ConflictException("Can only save data in Draft status");

        var submittedData = string.IsNullOrWhiteSpace(request.SubmittedData)
            ? new Dictionary<string, object>()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(request.SubmittedData) ?? new Dictionary<string, object>();

        submittedData[stepKey] = data;
        request.SubmittedData = JsonSerializer.Serialize(submittedData);
        
        // Update current step order if it's the next step
        if (int.TryParse(stepKey, out int stepOrder) && stepOrder > request.CurrentStepOrder)
        {
            request.CurrentStepOrder = stepOrder;
        }

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        return MapToDto(request);
    }

    public async Task<StudentRequestDto> SubmitRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        if (request.Status != RequestStatus.Draft)
            throw new ConflictException("Can only submit from Draft status");

        // Simple validation: must have completed at least one step if workflow has steps
        if (request.Service.Workflow != null && request.Service.Workflow.Steps.Any(s => s.StepType == WorkflowStepType.Form) && request.CurrentStepOrder == 0)
            throw new ConflictException("Please complete the required form data");

        if (request.Service.IsPaid)
        {
            request.Status = RequestStatus.PaymentPending;
            request.HistoryEntries.Add(new RequestHistoryEntry
            {
                Action = "Submitted",
                Comment = "Request submitted, awaiting payment",
                PerformedByUserId = request.StudentId,
                PerformedByRole = "Student",
                PerformedAt = DateTime.UtcNow
            });
        }
        else
        {
            request.Status = RequestStatus.Pending;
            request.SubmittedAt = DateTime.UtcNow;
            request.HistoryEntries.Add(new RequestHistoryEntry
            {
                Action = "Submitted",
                Comment = "Request submitted successfully",
                PerformedByUserId = request.StudentId,
                PerformedByRole = "Student",
                PerformedAt = DateTime.UtcNow
            });
        }

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        await NotifyStatusChange(request);

        return await GetStudentRequestAsync(requestId, cancellationToken);
    }

    public async Task<StudentRequestDto> ProcessPaymentAsync(Guid requestId, string paymentMethod, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        if (request.Status != RequestStatus.PaymentPending)
            throw new ConflictException("Request is not awaiting payment");

        // Mock payment processing
        request.PaymentStatus = PaymentStatus.Paid;
        request.AmountPaid = request.Service.Price;
        request.PaymentTransactionId = "MOCK-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        request.Status = RequestStatus.Pending;
        request.SubmittedAt = DateTime.UtcNow;

        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "PaymentCompleted",
            Comment = $"Payment processed via {paymentMethod}",
            PerformedByUserId = request.StudentId,
            PerformedByRole = "Student",
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        await NotifyStatusChange(request);

        return await GetStudentRequestAsync(requestId, cancellationToken);
    }

    public async Task<StudentRequestDto> AssignToStaffAsync(Guid requestId, Guid staffId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        request.AssignedToStaffId = staffId;
        request.AssignedAt = DateTime.UtcNow;
        
        if (request.Status == RequestStatus.Pending)
        {
            request.Status = RequestStatus.UnderReview;
        }

        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "Assigned",
            Comment = "Request assigned to staff",
            PerformedByUserId = staffId,
            PerformedByRole = "Staff",
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        await NotifyStatusChange(request);

        return await GetStudentRequestAsync(requestId, cancellationToken);
    }

    public async Task<StudentRequestDto> UpdateStatusAsync(Guid requestId, RequestStatus newStatus, string? comment = null, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        if (!CanTransition(request.Status, newStatus))
            throw new ConflictException($"Cannot transition from {request.Status} to {newStatus}");

        request.Status = newStatus;
        if (newStatus == RequestStatus.Completed)
        {
            request.CompletedAt = DateTime.UtcNow;
        }

        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "StatusChanged",
            Comment = comment ?? $"Status updated to {newStatus}",
            PerformedByUserId = _currentUser.Id,
            PerformedByRole = _currentUser.Role == "Staff" ? "Staff" : "Student",
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        await NotifyStatusChange(request);

        if (!string.IsNullOrWhiteSpace(comment))
        {
            await _notificationService.EnqueueNotificationAsync(request.StudentId, 
                "Status Update",
                $"{request.Service.Name}: {comment}",
                NotificationType.Info, cancellationToken);
        }

        return await GetStudentRequestAsync(requestId, cancellationToken);
    }

    public async Task<StudentRequestDto> AddCommentAsync(Guid requestId, string comment, Guid? performedByUserId, string role, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "CommentAdded",
            Comment = comment,
            PerformedByUserId = performedByUserId ?? _currentUser.Id,
            PerformedByRole = role,
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        return await GetStudentRequestAsync(requestId, cancellationToken);
    }

    public async Task<List<StaffRequestListItemDto>> GetPendingAssignmentsAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.GetAssignedToStaffAsync(staffId, cancellationToken);
        return requests.Where(x => x.Status == RequestStatus.UnderReview).ToList();
    }

    public async Task<List<StaffRequestListItemDto>> GetAssignedToStaffAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        return await _requestRepository.GetAssignedToStaffAsync(staffId, cancellationToken);
    }

    public async Task<PagedResult<StaffRequestListItemDto>> GetPagedRequestsForStaffAsync(int page, int pageSize, string? search, string? sortBy, bool ascending, Guid? staffId = null, CancellationToken cancellationToken = default)
    {
        var result = await _requestRepository.GetPagedRequestsForStaffAsync(page, pageSize, search, sortBy, ascending, staffId, cancellationToken);

        // Filter items in-memory based on scope — StudentId is already populated in the DTO
        var filteredItems = new List<StaffRequestListItemDto>();
        foreach (var item in result.Items)
        {
            if (await _effectiveScope.CanAccessStudentAsync(item.StudentId, cancellationToken))
            {
                filteredItems.Add(item);
            }
        }

        result.Items = filteredItems;
        result.TotalCount = filteredItems.Count;
        result.TotalPages = (int)Math.Ceiling(result.TotalCount / (double)pageSize);

        return result;
    }

    public async Task<List<RequestAttachmentDto>> GetAttachmentsByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        var attachments = await _requestRepository.GetAttachmentsByRequestIdAsync(requestId, cancellationToken);
        return attachments.Select(a => new RequestAttachmentDto
        {
            Id = a.Id,
            StudentRequestId = a.StudentRequestId,
            StepKey = a.StepKey,
            FileName = a.FileName,
            FilePath = a.FilePath,
            FileSize = a.FileSize,
            MimeType = a.MimeType,
            CreatedAt = a.CreatedAt
        }).ToList();
    }

    public async Task<StudentRequestDto?> GetPendingRequestForStudentAndServiceAsync(Guid studentId, Guid serviceId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetPendingForStudentAndServiceAsync(studentId, serviceId, cancellationToken);
        return request != null ? MapToDto(request) : null;
    }

    public async Task<StudentRequestDto> CloseRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        request.Close();
        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        return await GetStudentRequestAsync(requestId, cancellationToken);
    }

    public async Task<StudentRequestDto> OpenRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        request.Reopen();
        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        return await GetStudentRequestAsync(requestId, cancellationToken);
    }

    private async Task EnsureAccessAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (!await _effectiveScope.CanAccessStudentAsync(studentId, cancellationToken))
            throw new NotFoundException(LocalizedKeys.StudentInformation.StudentNotFound);
    }

    private async Task NotifyStatusChange(StudentRequest request)
    {
        await _hubContext.Clients.User(request.StudentId.ToString()).SendAsync("RequestStatusUpdated", request.Id, request.Status.ToString());
        
        if (request.AssignedToStaffId.HasValue)
        {
            await _hubContext.Clients.User(request.AssignedToStaffId.Value.ToString()).SendAsync("AssignedRequestUpdated", request.Id, request.Status.ToString());
        }
    }

    private bool CanTransition(RequestStatus current, RequestStatus next)
    {
        if (current == next) return true;
        if (current == RequestStatus.Cancelled || current == RequestStatus.Rejected || current == RequestStatus.Completed) return false;

        if (next == RequestStatus.Cancelled) return true;

        if (current == RequestStatus.Draft) return next == RequestStatus.Pending || next == RequestStatus.PaymentPending;
        if (current == RequestStatus.PaymentPending) return next == RequestStatus.Pending;
        if (current == RequestStatus.Pending) return next == RequestStatus.UnderReview || next == RequestStatus.Rejected;
        if (current == RequestStatus.UnderReview) return next == RequestStatus.Approved || next == RequestStatus.Rejected || next == RequestStatus.MoreInfoRequired;
        if (current == RequestStatus.MoreInfoRequired) return next == RequestStatus.UnderReview;
        if (current == RequestStatus.Approved) return next == RequestStatus.Completed || next == RequestStatus.ReadyForPickup;
        if (current == RequestStatus.ReadyForPickup) return next == RequestStatus.Completed;

        return false;
    }

    private StudentRequestDto MapToDto(StudentRequest request)
    {
        return new StudentRequestDto
        {
            Id = request.Id,
            ServiceId = request.ServiceId,
            ServiceName = request.Service.Name,
            StudentName = request.StudentNameJson,
            StudentCode = request.StudentCode,
            Status = request.Status,
            PaymentStatus = request.PaymentStatus,
            AmountPaid = request.AmountPaid,
            SubmittedAt = request.SubmittedAt,
            CompletedAt = request.CompletedAt,
            CreatedAt = request.CreatedAt,
            CurrentStepOrder = request.CurrentStepOrder,
            AssignedToStaffId = request.AssignedToStaffId,
            AssignedAt = request.AssignedAt,
            WorkflowSteps = request.Service.Workflow?.Steps.OrderBy(s => s.Order).Select(s => new StepInfoDto
            {
                Title = s.Title,
                Order = s.Order,
                StepType = (int)s.StepType
            }).ToList() ?? new List<StepInfoDto>(),
            History = request.HistoryEntries.OrderByDescending(h => h.PerformedAt).Select(h => new HistoryEntryDto
            {
                Action = h.Action,
                Comment = h.Comment,
                PerformedByUserId = h.PerformedByUserId,
                PerformedByRole = h.PerformedByRole,
                PerformedAt = h.PerformedAt
            }).ToList()
        };
    }
}
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
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

    public async Task<List<StudentRequestDto>> GetAllRequestsForStaffAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.GetAllForStaffAsync(cancellationToken);
        
        // Apply scope filtering in-memory as per existing repository pattern
        var filteredRequests = new List<StudentRequest>();
        foreach (var req in requests)
        {
            if (await _effectiveScope.CanAccessStudentAsync(req.StudentId, cancellationToken))
            {
                filteredRequests.Add(req);
            }
        }

        return filteredRequests.Select(MapToDto).ToList();
    }

    public async Task<StudentRequestDto> CreateDraftAsync(Guid studentId, Guid serviceId, CancellationToken cancellationToken = default)
    {
        await EnsureAccessAsync(studentId, cancellationToken);

        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken);
        if (service == null) throw new NotFoundException("Service not found");
        if (!service.IsActive) throw new InvalidOperationException("Service is not active");

        var request = new StudentRequest
        {
            StudentId = studentId,
            ServiceId = serviceId,
            Status = RequestStatus.Draft,
            PaymentStatus = service.IsPaid ? PaymentStatus.Pending : PaymentStatus.NotRequired,
            AmountPaid = null,
            SubmittedData = "{}",
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
        return MapToDto(request);
    }

    public async Task<StudentRequestDto> SaveStepDataAsync(Guid requestId, string stepKey, object data, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        request.EnsureMutable();

        if (request.Status != RequestStatus.Draft && request.Status != RequestStatus.MoreInfoRequired && request.Status != RequestStatus.PaymentPending)
            throw new InvalidOperationException("Cannot modify data at this status");

        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(request.SubmittedData) ?? new Dictionary<string, object>();

        if (data is JsonElement jsonElement)
        {
            var fieldValues = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
            if (fieldValues != null)
            {
                foreach (var kv in fieldValues)
                    dict[kv.Key] = kv.Value;
            }
        }
        else if (data is Dictionary<string, object> fieldDict)
        {
            foreach (var kv in fieldDict)
                dict[kv.Key] = kv.Value;
        }
        else
        {
            dict[stepKey] = data;
        }

        request.SubmittedData = JsonSerializer.Serialize(dict);

        if (int.TryParse(stepKey, out int stepOrder))
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

        request.EnsureMutable();

        if (request.Service.IsPaid && request.PaymentStatus != PaymentStatus.Paid)
        {
            request.Status = RequestStatus.PaymentPending;
            request.HistoryEntries.Add(new RequestHistoryEntry
            {
                Action = "PaymentRequired",
                Comment = "Payment is required to submit",
                PerformedByUserId = request.StudentId,
                PerformedByRole = "Student",
                PerformedAt = DateTime.UtcNow
            });
            _requestRepository.Update(request);
            await _requestRepository.SaveChangesAsync(cancellationToken);
            return MapToDto(request);
        }

        var workflow = request.Service.Workflow;
        if (workflow == null) throw new InvalidOperationException("Service has no workflow defined");

        // Only Form steps are required for submission (Review and Payment are optional)
        var requiredStepOrders = workflow.Steps
            .Where(s => s.IsRequired && s.StepType == WorkflowStepType.Form)
            .Select(s => s.Order.ToString())
            .ToList();

        var submittedDict = JsonSerializer.Deserialize<Dictionary<string, object>>(request.SubmittedData) ?? new Dictionary<string, object>();
        var missingSteps = requiredStepOrders.Except(submittedDict.Keys).ToList();
        if (missingSteps.Any())
            throw new ValidationException($"Missing required steps: {string.Join(", ", missingSteps)}");

        request.Status = RequestStatus.Pending;
        request.SubmittedAt = DateTime.UtcNow;
        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "Submitted",
            Comment = "Student submitted the request",
            PerformedByUserId = request.StudentId,
            PerformedByRole = "Student",
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateNotificationAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "New Student Request",
            $"Student {request.StudentId} submitted a request for service '{request.Service.Name}'",
            NotificationType.Info);
        await _hubContext.Clients.Group("staff-notifications").SendAsync("NewRequestReceived", new { requestId, serviceName = request.Service.Name });

        await _notificationService.CreateNotificationAsync(
            request.StudentId,
            "Request Submitted",
            $"Your request #{request.RequestNumber} has been submitted successfully.",
            NotificationType.Info);

        return MapToDto(request);
    }

    public async Task<StudentRequestDto> ProcessPaymentAsync(Guid requestId, string paymentMethod, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        request.EnsureMutable();

        if (request.Status != RequestStatus.PaymentPending)
            throw new InvalidOperationException("Request is not in payment pending state");

        request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);

        request.PaymentStatus = PaymentStatus.Paid;
        request.AmountPaid = request.Service?.Price ?? 0;
        request.PaymentTransactionId = Guid.NewGuid().ToString();
        request.Status = RequestStatus.Draft;

        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "PaymentCompleted",
            Comment = $"Payment of {request.AmountPaid} completed via {paymentMethod}",
            PerformedByUserId = request.StudentId,
            PerformedByRole = "Student",
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateNotificationAsync(
            request.StudentId,
            "Payment Successful",
            $"Your payment of ${request.AmountPaid} for request #{request.RequestNumber} was successful.",
            NotificationType.Info);

        await _notificationService.CreateNotificationAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "New Paid Student Request",
            $"Student {request.StudentId} submitted and paid for request #{request.RequestNumber}",
            NotificationType.Info);
        await _hubContext.Clients.Group("staff-notifications").SendAsync("NewRequestReceived", new { requestId, serviceName = request.Service.Name });

        return MapToDto(request);
    }

    public async Task<StudentRequestDto> AssignToStaffAsync(Guid requestId, Guid staffId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        request.EnsureMutable();

        if (request.Status != RequestStatus.Pending && request.Status != RequestStatus.UnderReview)
            throw new InvalidOperationException("Only pending or under-review requests can be assigned");

        request.AssignedToStaffId = staffId;
        request.AssignedAt = DateTime.UtcNow;
        request.Status = RequestStatus.UnderReview;

        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "Assigned",
            Comment = $"Assigned to staff {staffId}",
            PerformedByUserId = staffId,
            PerformedByRole = "Staff",
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateNotificationAsync(staffId, "Request Assigned", $"You have been assigned to request {requestId}", NotificationType.Info);
        await _hubContext.Clients.Group($"request-{requestId}").SendAsync("Assigned", new { staffId });

        return MapToDto(request);
    }

    public async Task<StudentRequestDto> UpdateStatusAsync(Guid requestId, RequestStatus newStatus, string? comment = null, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        var userRole = _currentUser.Role ?? "Staff";
        
        // Workflow-aware guard: If the next step in the workflow requires a specific role, enforce it.
        var workflow = request.Service?.Workflow;
        if (workflow != null)
        {
            var currentStep = workflow.Steps.FirstOrDefault(s => s.Order == request.CurrentStepOrder);
            var nextStep = workflow.Steps.OrderBy(s => s.Order).FirstOrDefault(s => s.Order > request.CurrentStepOrder);

            // If we are moving to a "Review" related status, check if the current or next step allows it for this role
            if (IsReviewStatus(newStatus))
            {
                var reviewStep = nextStep?.StepType == WorkflowStepType.Review ? nextStep : (currentStep?.StepType == WorkflowStepType.Review ? currentStep : null);
                if (reviewStep != null && reviewStep.TransitionType == WorkflowTransitionType.Staff && userRole != "Staff" && userRole != "Admin")
                {
                    throw new UnauthorizedAccessException("Only staff can perform review-related transitions for this workflow step.");
                }
            }
        }

        if (!IsValidTransition(request.Status, newStatus, userRole))
            throw new InvalidOperationException($"Invalid status transition from {request.Status} to {newStatus} for role {userRole}");

        var oldStatus = request.Status;
        request.Status = newStatus;
        if (newStatus == RequestStatus.Completed)
            request.CompletedAt = DateTime.UtcNow;

        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = $"StatusChanged_{newStatus}",
            Comment = comment ?? $"Status changed from {oldStatus} to {newStatus}",
            PerformedByUserId = _currentUser.Id,
            PerformedByRole = userRole,
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateNotificationAsync(
            request.StudentId,
            $"Request Status Changed to {newStatus}",
            $"Your request #{request.RequestNumber} status has been updated to {newStatus}. Comment: {comment ?? "No comment"}",
            NotificationType.Info,
            cancellationToken: cancellationToken);

        if (newStatus == RequestStatus.Completed || newStatus == RequestStatus.Rejected || newStatus == RequestStatus.Cancelled)
        {
            request.Close();
            _requestRepository.Update(request);
            await _requestRepository.SaveChangesAsync(cancellationToken);
        }

        return MapToDto(request);
    }

    public async Task<StudentRequestDto> AddCommentAsync(Guid requestId, string comment, Guid? performedByUserId, string role, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        request.EnsureMutable();

        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "Comment",
            Comment = comment,
            PerformedByUserId = performedByUserId,
            PerformedByRole = role,
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.Group($"request-{requestId}").SendAsync("NewComment", new { comment, performedByUserId, role });

        return MapToDto(request);
    }

    public async Task<List<StudentRequestDto>> GetPendingAssignmentsAsync(Guid staffId, CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.GetAssignedToStaffAsync(staffId, cancellationToken);
        return requests.Where(x => x.Status == RequestStatus.UnderReview).Select(MapToDto).ToList();
    }

    public async Task<PagedResult<StaffRequestListItemDto>> GetPagedRequestsForStaffAsync(int page, int pageSize, string? search, string? sortBy, bool ascending, CancellationToken cancellationToken = default)
    {
        var result = await _requestRepository.GetPagedRequestsForStaffAsync(page, pageSize, search, sortBy, ascending, cancellationToken);

        // Filter items in-memory based on scope
        var filteredItems = new List<StaffRequestListItemDto>();
        foreach (var item in result.Items)
        {
            var request = await _requestRepository.GetByIdAsync(item.Id, cancellationToken);
            if (request != null && await _effectiveScope.CanAccessStudentAsync(request.StudentId, cancellationToken))
            {
                filteredItems.Add(item);
            }
        }

        result.Items = filteredItems;
        result.TotalCount = filteredItems.Count; // Adjust count based on filtered results
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
        await EnsureAccessAsync(studentId, cancellationToken);

        var request = await _requestRepository.GetPendingForStudentAndServiceAsync(studentId, serviceId, cancellationToken);
        return request == null ? null : MapToDto(request);
    }

    public async Task<StudentRequestDto> CloseRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        request.Close();
        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "Closed",
            Comment = "Record closed manually",
            PerformedByUserId = _currentUser.Id,
            PerformedByRole = _currentUser.Role ?? "Staff",
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);
        return MapToDto(request);
    }

    public async Task<StudentRequestDto> OpenRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        await EnsureAccessAsync(request.StudentId, cancellationToken);

        request.Reopen();
        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = "Reopened",
            Comment = "Record reopened manually",
            PerformedByUserId = _currentUser.Id,
            PerformedByRole = _currentUser.Role ?? "Staff",
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);
        return MapToDto(request);
    }

    private async Task EnsureAccessAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (!await _effectiveScope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            throw new NotFoundException("Request not found");
        }
    }

    private bool IsReviewStatus(RequestStatus status)
    {
        return status == RequestStatus.Approved ||
               status == RequestStatus.Rejected ||
               status == RequestStatus.UnderReview ||
               status == RequestStatus.MoreInfoRequired;
    }

    private bool IsValidTransition(RequestStatus current, RequestStatus next, string role)
    {
        // Basic state machine validation
        bool isBasicValid = (current, next) switch
        {
            (RequestStatus.Draft, RequestStatus.Pending) => true,
            (RequestStatus.Draft, RequestStatus.Cancelled) => true,
            (RequestStatus.Pending, RequestStatus.UnderReview) => true,
            (RequestStatus.Pending, RequestStatus.Rejected) => true,
            (RequestStatus.UnderReview, RequestStatus.Approved) => true,
            (RequestStatus.UnderReview, RequestStatus.MoreInfoRequired) => true,
            (RequestStatus.UnderReview, RequestStatus.Rejected) => true,
            (RequestStatus.MoreInfoRequired, RequestStatus.Pending) => true,
            (RequestStatus.MoreInfoRequired, RequestStatus.Cancelled) => true,
            (RequestStatus.Approved, RequestStatus.ReadyForPickup) => true,
            (RequestStatus.ReadyForPickup, RequestStatus.Completed) => true,
            (RequestStatus.Approved, RequestStatus.Completed) => true,
            (RequestStatus.PaymentPending, RequestStatus.Approved) => true,
            (RequestStatus.PaymentPending, RequestStatus.Completed) => true,
            (_, RequestStatus.Cancelled) => true,
            _ => false
        };

        if (!isBasicValid) return false;

        // Role-based guards
        if (role == "Student")
        {
            // Students can only submit (Draft -> Pending) or cancel
            return next == RequestStatus.Pending || next == RequestStatus.Cancelled;
        }

        if (role == "Staff" || role == "Admin")
        {
            // Staff cannot "un-submit" a request to Draft (unless specifically allowed by business logic, but usually it's MoreInfoRequired)
            if (next == RequestStatus.Draft) return false;
            return true;
        }

        return false;
    }

    private StudentRequestDto MapToDto(StudentRequest request)
    {
        return new StudentRequestDto
        {
            Id = request.Id,
            ServiceId = request.ServiceId,
            ServiceName = request.Service?.Name ?? string.Empty,
            StudentCode = request.StudentCode,
            StudentName = request.StudentNameJson,
            Status = request.Status,
            PaymentStatus = request.PaymentStatus,
            AmountPaid = request.AmountPaid,
            CurrentStepOrder = request.CurrentStepOrder,
            SubmittedAt = request.SubmittedAt,
            CompletedAt = request.CompletedAt,
            RequestNumber = request.RequestNumber,
            ServicePrice = request.Service?.Price,
            IsClosed = request.IsClosed,
            ClosedAt = request.ClosedAt,
            History = request.HistoryEntries.Select(h => new HistoryEntryDto
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
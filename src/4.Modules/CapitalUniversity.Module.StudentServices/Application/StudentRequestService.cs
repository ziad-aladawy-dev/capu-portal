using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.Hubs;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace CapitalUniversity.Module.StudentServices.Application;

public class StudentRequestService : IStudentRequestService
{
    private readonly IStudentRequestRepository _requestRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly INotificationService _notificationService;
    private readonly IHubContext<StudentServicesHub> _hubContext;

    public StudentRequestService(
        IStudentRequestRepository requestRepository,
        IServiceRepository serviceRepository,
        INotificationService notificationService,
        IHubContext<StudentServicesHub> hubContext)
    {
        _requestRepository = requestRepository;
        _serviceRepository = serviceRepository;
        _notificationService = notificationService;
        _hubContext = hubContext;
    }

    public async Task<StudentRequestDto> GetRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");
        return MapToDto(request);
    }

    public async Task<List<StudentRequestDto>> GetStudentRequestsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.GetByStudentIdAsync(studentId, cancellationToken);
        return requests.Select(MapToDto).ToList();
    }

    public async Task<StudentRequestDto> CreateDraftAsync(Guid studentId, Guid serviceId, CancellationToken cancellationToken = default)
    {
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
        if (request.Status != RequestStatus.Draft && request.Status != RequestStatus.MoreInfoRequired)
            throw new InvalidOperationException("Cannot modify data at this status");

        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(request.SubmittedData) ?? new Dictionary<string, object>();
        dict[stepKey] = data;
        request.SubmittedData = JsonSerializer.Serialize(dict);

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);
        return MapToDto(request);
    }

    public async Task<StudentRequestDto> SubmitRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdWithDetailsAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");
        if (request.Status != RequestStatus.Draft && request.Status != RequestStatus.MoreInfoRequired)
            throw new InvalidOperationException("Only draft or more-info-required requests can be submitted");

        if (request.Service.IsPaid && request.PaymentStatus != PaymentStatus.Paid)
        {
            request.Status = RequestStatus.PaymentPending;
            request.HistoryEntries.Add(new RequestHistoryEntry
            {
                Action = "PaymentPending",
                Comment = "Awaiting payment confirmation",
                PerformedByUserId = request.StudentId,
                PerformedByRole = "Student",
                PerformedAt = DateTime.UtcNow
            });
            _requestRepository.Update(request);
            await _requestRepository.SaveChangesAsync(cancellationToken);
            return MapToDto(request);
        }

        var workflow = request.Service.Workflow;
        var requiredSteps = workflow.Steps.Where(s => s.IsRequired).Select(s => s.StepKey).ToList();
        var submittedDict = JsonSerializer.Deserialize<Dictionary<string, object>>(request.SubmittedData) ?? new Dictionary<string, object>();
        var missingSteps = requiredSteps.Except(submittedDict.Keys).ToList();
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

        return MapToDto(request);
    }

    public async Task<StudentRequestDto> AssignToStaffAsync(Guid requestId, Guid staffId, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");
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
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        if (!IsValidTransition(request.Status, newStatus))
            throw new InvalidOperationException($"Invalid status transition from {request.Status} to {newStatus}");

        var oldStatus = request.Status;
        request.Status = newStatus;

        if (newStatus == RequestStatus.Completed)
            request.CompletedAt = DateTime.UtcNow;

        request.HistoryEntries.Add(new RequestHistoryEntry
        {
            Action = $"StatusChanged_{newStatus}",
            Comment = comment ?? $"Status changed from {oldStatus} to {newStatus}",
            PerformedByUserId = null,
            PerformedByRole = "Staff",
            PerformedAt = DateTime.UtcNow
        });

        _requestRepository.Update(request);
        await _requestRepository.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateNotificationAsync(request.StudentId, $"Request {newStatus}", $"Your request {requestId} status changed to {newStatus}", NotificationType.Info);
        await _hubContext.Clients.Group($"request-{requestId}").SendAsync("StatusUpdated", new { status = newStatus, comment });

        return MapToDto(request);
    }

    public async Task<StudentRequestDto> AddCommentAsync(Guid requestId, string comment, Guid? performedByUserId, string role, CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

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

    private bool IsValidTransition(RequestStatus current, RequestStatus next)
    {
        return (current, next) switch
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
    }

    private StudentRequestDto MapToDto(StudentRequest request)
    {
        return new StudentRequestDto
        {
            Id = request.Id,
            StudentId = request.StudentId,
            ServiceId = request.ServiceId,
            ServiceName = request.Service?.Name ?? string.Empty,
            Status = request.Status,
            PaymentStatus = request.PaymentStatus,
            AmountPaid = request.AmountPaid,
            CurrentStepOrder = request.CurrentStepOrder,
            AssignedToStaffId = request.AssignedToStaffId,
            SubmittedAt = request.SubmittedAt,
            CompletedAt = request.CompletedAt,
            CreatedAt = request.CreatedAt,
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
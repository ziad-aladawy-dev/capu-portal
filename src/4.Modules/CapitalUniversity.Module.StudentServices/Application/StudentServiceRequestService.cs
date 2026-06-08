using System.Globalization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using CapitalUniversity.Modules.StudentServices.Domain;
using CapitalUniversity.Modules.StudentServices.Repositories;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Modules.StudentServices.Application;

/// <summary>
/// Bundle of validators for the request lifecycle endpoints. Same rationale
/// as <see cref="StudentServiceValidators"/>.
/// </summary>
public sealed record StudentServiceRequestValidators(
    IValidator<SubmitStudentServiceRequestRequest> Submit,
    IValidator<CancelStudentServiceRequestRequest> Cancel,
    IValidator<ApproveStudentServiceRequestRequest> Approve,
    IValidator<RejectStudentServiceRequestRequest> Reject,
    IValidator<MoveRequestWorkflowStateRequest> MoveState);

/// <summary>
/// Owns the request lifecycle. The orchestration shape:
///   <list type="number">
///     <item>Student submits — validated against the service's configured fields + documents.</item>
///     <item>If the service charges a fee, an invoice is authored via <see cref="IFeeCreationService"/>
///       and the request moves to <see cref="ServiceRequestStatus.WaitingPayment"/>.</item>
///     <item>On payment confirmation (<see cref="ConfirmPaymentAsync"/>) the request advances
///       through the configured automatic transition (typically to <see cref="ServiceRequestStatus.UnderReview"/>).</item>
///     <item>Staff approve / reject / advance via the workflow's manual transitions.</item>
///   </list>
///
/// <para>
/// Scope is enforced through <see cref="IEffectiveScope.CanAccessStudentAsync"/> on every read +
/// write path, matching the StudentProfileRecord / Invoice convention. Out-of-scope or missing
/// rows both surface as <c>NotFound</c> — no existence leak.
/// </para>
/// </summary>
public class StudentServiceRequestService : IStudentServiceRequestService
{
    internal const string CacheKeyPrefix = "student-service-request:object:";
    // List caches keyed by a version stamp; any request lifecycle transition
    // rotates it. Per-row scope filtering still runs on read.
    internal const string CollectionVersionKey = "student-service-request:coll:ver";
    internal const string ListKeyPrefix = "student-service-request:list:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentServiceRequestRepository _requests;
    private readonly IStudentServiceRepository _services;
    private readonly IWorkflowService _workflows;
    private readonly IFeeGenerationService _feeGeneration;
    private readonly IEffectiveScope _scope;
    private readonly ICacheService _cache;
    private readonly INotificationService _notifications;
    private readonly IAppLogger _logger;
    private readonly ILocalizationService _localization;
    private readonly StudentServiceRequestValidators _validators;

    public StudentServiceRequestService(
        IUnitOfWork unitOfWork,
        IStudentServiceRequestRepository requests,
        IStudentServiceRepository services,
        IWorkflowService workflows,
        IFeeGenerationService feeGeneration,
        IEffectiveScope scope,
        ICacheService cache,
        INotificationService notifications,
        IAppLogger logger,
        ILocalizationService localization,
        StudentServiceRequestValidators validators)
    {
        _unitOfWork = unitOfWork;
        _requests = requests;
        _services = services;
        _workflows = workflows;
        _feeGeneration = feeGeneration;
        _scope = scope;
        _cache = cache;
        _notifications = notifications;
        _logger = logger;
        _localization = localization;
        _validators = validators;
    }

    internal static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id:N}";

    public async Task<StudentServiceRequestResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetAsync<StudentServiceRequestResponse>(CacheKey(id), cancellationToken);
        if (cached is not null)
        {
            if (!await _scope.CanAccessStudentAsync(cached.StudentId, cancellationToken)) return null;
            return Localize(cached);
        }

        var request = await _requests.GetByIdAsync(id, includeChildren: true, cancellationToken);
        if (request is null) return null;
        if (!await _scope.CanAccessStudentAsync(request.StudentId, cancellationToken)) return null;

        var dto = MapToResponse(request);
        await _cache.SetAsync(CacheKey(id), dto, CacheTtl, cancellationToken);
        return Localize(dto);
    }

    /// <summary>
    /// Decode the bilingual service-name JSON for the current culture. Same
    /// read-time projection convention as ScheduleSlotService / StudentServiceService.
    /// </summary>
    private StudentServiceRequestResponse Localize(StudentServiceRequestResponse response)
    {
        if (!string.IsNullOrEmpty(response.ServiceName))
        {
            response.ServiceName = _localization.Get<string>(response.ServiceName);
        }
        return response;
    }

    private StudentServiceRequestSummaryResponse LocalizeSummary(StudentServiceRequestSummaryResponse response)
    {
        if (!string.IsNullOrEmpty(response.ServiceName))
        {
            response.ServiceName = _localization.Get<string>(response.ServiceName);
        }
        return response;
    }

    public Task<PagedResponse<StudentServiceRequestSummaryResponse>> ListAsync(StudentServiceRequestListQuery query, CancellationToken cancellationToken = default) =>
        CachedListAsync(query, statusInclude: null, discriminator: "all", cancellationToken);

    public Task<PagedResponse<StudentServiceRequestSummaryResponse>> ListAssignedToStaffAsync(Guid staffId, StudentServiceRequestListQuery query, CancellationToken cancellationToken = default)
    {
        query.AssignedStaffId = staffId;
        return CachedListAsync(query, statusInclude: null, discriminator: "assigned", cancellationToken);
    }

    public Task<PagedResponse<StudentServiceRequestSummaryResponse>> ListPendingAsync(StudentServiceRequestListQuery query, CancellationToken cancellationToken = default)
    {
        // Default sort for pending lists is oldest-first per the spec, but
        // a caller can override.
        query.SortBy ??= "submittedAt";
        query.SortAscending ??= true;

        var pending = new[]
        {
            ServiceRequestStatus.Submitted,
            ServiceRequestStatus.UnderReview,
            ServiceRequestStatus.WaitingPayment,
        };
        return CachedListAsync(query, statusInclude: pending, discriminator: "pending", cancellationToken);
    }

    /// <summary>
    /// Shared cached read-through for the three list endpoints. The repository
    /// page (projected to culture-neutral summaries + the unfiltered total) is
    /// cached under the collection version + a discriminator + the query
    /// signature; the per-row scope filter and localization run on read so the
    /// expensive query is shared while each caller still sees only permitted rows
    /// (preserving the prior "unfiltered total, filtered items" behaviour).
    /// </summary>
    private async Task<PagedResponse<StudentServiceRequestSummaryResponse>> CachedListAsync(
        StudentServiceRequestListQuery query,
        ServiceRequestStatus[]? statusInclude,
        string discriminator,
        CancellationToken cancellationToken)
    {
        var version = await GetCollectionVersionAsync(cancellationToken);
        var key = $"{ListKeyPrefix}{version}:{discriminator}:{BuildSignature(query)}";
        var cached = await _cache.GetOrSetAsync<PagedResponse<StudentServiceRequestSummaryResponse>>(
            key,
            async ct =>
            {
                var (items, total) = await _requests.ListAsync(query, statusInclude, ct);
                return new PagedResponse<StudentServiceRequestSummaryResponse>
                {
                    Items = items.Select(MapToSummary).ToList(),
                    Page = query.Page,
                    PageSize = query.PageSize,
                    TotalCount = total,
                };
            },
            CacheTtl,
            cancellationToken)
            ?? new PagedResponse<StudentServiceRequestSummaryResponse> { Items = new List<StudentServiceRequestSummaryResponse>(), Page = query.Page, PageSize = query.PageSize };

        var visible = new List<StudentServiceRequestSummaryResponse>(cached.Items.Count);
        foreach (var s in cached.Items)
        {
            if (await _scope.CanAccessStudentAsync(s.StudentId, cancellationToken))
            {
                visible.Add(LocalizeSummaryCopy(s));
            }
        }

        return new PagedResponse<StudentServiceRequestSummaryResponse>
        {
            Items = visible,
            Page = cached.Page,
            PageSize = cached.PageSize,
            TotalCount = cached.TotalCount,
        };
    }

    private async Task<string> GetCollectionVersionAsync(CancellationToken cancellationToken)
    {
        var v = await _cache.GetAsync<string>(CollectionVersionKey, cancellationToken);
        if (!string.IsNullOrEmpty(v)) return v;
        await _cache.SetAsync(CollectionVersionKey, "0", VersionTtl, cancellationToken);
        return "0";
    }

    private Task BumpCollectionVersionAsync(CancellationToken cancellationToken) =>
        _cache.SetAsync(CollectionVersionKey, Guid.NewGuid().ToString("N"), VersionTtl, cancellationToken);

    private static string BuildSignature(StudentServiceRequestListQuery q) =>
        string.Join('|',
            $"status={(q.Status.HasValue ? ((int)q.Status.Value).ToString() : string.Empty)}",
            $"svc={q.StudentServiceId?.ToString("N") ?? string.Empty}",
            $"stu={q.StudentId?.ToString("N") ?? string.Empty}",
            $"staff={q.AssignedStaffId?.ToString("N") ?? string.Empty}",
            $"from={q.SubmittedFrom?.ToString("O") ?? string.Empty}",
            $"to={q.SubmittedTo?.ToString("O") ?? string.Empty}",
            $"sortby={q.SortBy ?? string.Empty}",
            $"asc={q.SortAscending?.ToString() ?? string.Empty}",
            $"search={q.Search ?? string.Empty}",
            $"p={q.Page}",
            $"ps={q.PageSize}");

    /// <summary>Localize onto a NEW summary so cached list entries are never mutated.</summary>
    private StudentServiceRequestSummaryResponse LocalizeSummaryCopy(StudentServiceRequestSummaryResponse s) => new()
    {
        Id = s.Id,
        StudentId = s.StudentId,
        StudentServiceId = s.StudentServiceId,
        ServiceCode = s.ServiceCode,
        ServiceName = string.IsNullOrEmpty(s.ServiceName) ? s.ServiceName : _localization.Get<string>(s.ServiceName),
        CurrentStatus = s.CurrentStatus,
        SubmittedAt = s.SubmittedAt,
        AssignedStaffId = s.AssignedStaffId,
        PaymentReferenceId = s.PaymentReferenceId,
        CreatedAt = s.CreatedAt,
    };

    public async Task<Guid> SubmitAsync(Guid studentId, SubmitStudentServiceRequestRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.Submit.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            // Defensive: a student can only submit on their own behalf. The
            // controller already binds studentId from the JWT, so this is a
            // belt-and-suspenders guard against impersonation by a future
            // misconfigured route.
            throw new NotFoundException(LocalizedKeys.StudentServices.ServiceNotFound);
        }

        var service = await _services.GetByIdAsync(request.StudentServiceId, includeChildren: true, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.StudentServices.ServiceNotFound);

        if (!service.IsActive)
        {
            throw new ConflictException(LocalizedKeys.StudentServices.ServiceInactive);
        }

        ValidateDynamicFieldsAndDocuments(service, request);

        // Resolve the initial state from the configured workflow when one is
        // attached; fall back to Submitted otherwise.
        var initialStatus = await ResolveInitialStatusAsync(service, cancellationToken);

        var entity = new StudentServiceRequest
        {
            StudentId = studentId,
            StudentServiceId = service.Id,
            CurrentStatus = initialStatus,
            SubmittedAt = DateTime.UtcNow,
        };

        foreach (var v in request.FieldValues)
        {
            entity.FieldValues.Add(new ServiceFieldValue
            {
                FieldDefinitionId = v.FieldDefinitionId,
                Value = v.Value ?? string.Empty,
            });
        }
        foreach (var f in request.Files)
        {
            entity.Documents.Add(new ServiceDocumentSubmission
            {
                DocumentDefinitionId = f.DocumentDefinitionId,
                FileName = f.FileName,
                StoredFileName = f.StoredFileName,
                FilePath = f.FilePath,
                ContentType = f.ContentType,
                FileSize = f.FileSize,
            });
        }

        await _requests.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fee integration — only the Payments module owns invoice authoring;
        // we drop a fee through IFeeCreationService and stash the invoice id
        // on the request. Status is moved to WaitingPayment if not already
        // there.
        if (service.RequiresPayment)
        {
            // Treasury fee path: generate a StudentFee from the service's receipt
            // mapping (price snapshotted from the receipt). Returns null when the
            // service has no active mapping — then no fee is raised and the
            // request proceeds without a payment step.
            var treasuryFeeId = await _feeGeneration.GenerateFeeFromServiceAsync(
                studentId,
                service.Id,
                request.Quantity,
                "student-services",
                entity.Id,
                cancellationToken);

            if (treasuryFeeId is not null)
            {
                entity.PaymentReferenceId = treasuryFeeId.Value;
                entity.CurrentStatus = ServiceRequestStatus.WaitingPayment;
                _requests.Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        await _logger.LogInfoAsync(
            $"student-service.request submitted id={entity.Id} service={service.Code}",
            nameof(StudentServiceRequestService),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["RequestId"] = entity.Id,
                ["StudentId"] = studentId,
                ["ServiceId"] = service.Id,
                ["Status"] = entity.CurrentStatus.ToString(),
                ["InvoiceId"] = entity.PaymentReferenceId?.ToString() ?? string.Empty,
            });

        // A new request must surface in the list/pending/assigned reads.
        await BumpCollectionVersionAsync(cancellationToken);
        return entity.Id;
    }

    public async Task CancelAsync(Guid studentId, Guid requestId, CancelStudentServiceRequestRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.Cancel.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var entity = await LoadForStudentAsync(studentId, requestId, cancellationToken);

        // Students can only cancel before staff processing begins. Approve /
        // Reject / Completed all count as "processing begun"; UnderReview also
        // counts since a reviewer is already engaged.
        if (entity.CurrentStatus is ServiceRequestStatus.UnderReview
                                 or ServiceRequestStatus.Approved
                                 or ServiceRequestStatus.Rejected
                                 or ServiceRequestStatus.Completed
                                 or ServiceRequestStatus.Cancelled)
        {
            throw new ConflictException(LocalizedKeys.StudentServices.CannotCancelAfterProcessing);
        }

        entity.CurrentStatus = ServiceRequestStatus.Cancelled;
        entity.CancellationReason = request.Reason;
        entity.UpdatedAt = DateTime.UtcNow;
        _requests.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(requestId), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);

        await _logger.LogInfoAsync(
            $"student-service.request cancelled id={requestId}",
            nameof(StudentServiceRequestService),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
                ["StudentId"] = studentId,
                ["Reason"] = request.Reason,
            });
    }

    public async Task ApproveAsync(Guid requestId, Guid staffId, ApproveStudentServiceRequestRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.Approve.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var entity = await LoadForStaffAsync(requestId, cancellationToken);
        await EnsureTransitionAllowedAsync(entity, ServiceRequestStatus.Approved, WorkflowTransitionType.Manual, cancellationToken);

        entity.CurrentStatus = ServiceRequestStatus.Approved;
        entity.AssignedStaffId = staffId;
        entity.ProcessedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _requests.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(requestId), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);

        await NotifyStudentAsync(entity, "Request approved", request.Note ?? string.Empty, NotificationType.Info, cancellationToken);

        await _logger.LogInfoAsync(
            $"student-service.request approved id={requestId}",
            nameof(StudentServiceRequestService),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
                ["StaffId"] = staffId,
            });
    }

    public async Task RejectAsync(Guid requestId, Guid staffId, RejectStudentServiceRequestRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.Reject.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var entity = await LoadForStaffAsync(requestId, cancellationToken);
        await EnsureTransitionAllowedAsync(entity, ServiceRequestStatus.Rejected, WorkflowTransitionType.Manual, cancellationToken);

        entity.CurrentStatus = ServiceRequestStatus.Rejected;
        entity.AssignedStaffId = staffId;
        entity.ProcessedAt = DateTime.UtcNow;
        entity.RejectionReason = request.Reason;
        entity.UpdatedAt = DateTime.UtcNow;
        _requests.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(requestId), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);

        await NotifyStudentAsync(entity, "Request rejected", request.Reason, NotificationType.Warning, cancellationToken);

        await _logger.LogInfoAsync(
            $"student-service.request rejected id={requestId}",
            nameof(StudentServiceRequestService),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
                ["StaffId"] = staffId,
                ["Reason"] = request.Reason,
            });
    }

    public async Task<BulkActionResult> BulkTransitionAsync(
        IReadOnlyList<Guid> requestIds,
        Guid staffId,
        MoveRequestWorkflowStateRequest payload,
        CancellationToken cancellationToken = default)
    {
        var succeeded = new List<Guid>(requestIds.Count);
        var failures = new List<BulkActionFailure>();

        foreach (var id in requestIds.Distinct())
        {
            try
            {
                // Per-row dispatch keeps workflow validation, scope checks, and
                // audit logging identical to the single-row path — no shortcut
                // path that could bypass any of those.
                await MoveStateAsync(id, staffId, payload, cancellationToken);
                succeeded.Add(id);
            }
            catch (NotFoundException ex)
            {
                failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.NotFound, Message = ex.Message });
            }
            catch (ConflictException ex)
            {
                failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.InvalidTransition, Message = ex.Message });
            }
            catch (ValidationException ex)
            {
                failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.Validation, Message = ex.Message });
            }
        }

        return BulkActionResult.From(succeeded, failures);
    }

    public async Task MoveStateAsync(Guid requestId, Guid staffId, MoveRequestWorkflowStateRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.MoveState.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var entity = await LoadForStaffAsync(requestId, cancellationToken);
        await EnsureTransitionAllowedAsync(entity, request.TargetStatus, WorkflowTransitionType.Manual, cancellationToken);

        entity.CurrentStatus = request.TargetStatus;
        entity.AssignedStaffId = staffId;
        if (request.TargetStatus is ServiceRequestStatus.Completed or ServiceRequestStatus.Approved or ServiceRequestStatus.Rejected)
        {
            entity.ProcessedAt = DateTime.UtcNow;
        }
        entity.UpdatedAt = DateTime.UtcNow;
        _requests.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(requestId), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);

        await _logger.LogInfoAsync(
            $"student-service.request moved id={requestId} to={request.TargetStatus}",
            nameof(StudentServiceRequestService),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
                ["StaffId"] = staffId,
                ["TargetStatus"] = request.TargetStatus.ToString(),
            });
    }

    public async Task ConfirmPaymentAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var entity = await _requests.GetByIdAsync(requestId, includeChildren: false, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.StudentServices.RequestNotFound);

        // Idempotent on already-advanced requests: the outbox guarantees
        // at-least-once delivery of <c>payments.invoice.paid</c>, so a replay
        // would otherwise turn the second call into a 409. Treating it as a
        // no-op (logged) matches the at-least-once contract and keeps the
        // event handler from poisoning the outbox row on every retry.
        //
        // Cancelled / terminal states are also a no-op — a paid invoice
        // landing on a cancelled request shouldn't resurrect it; the refund
        // policy is a Payments concern.
        if (entity.CurrentStatus != ServiceRequestStatus.WaitingPayment)
        {
            await _logger.LogInfoAsync(
                $"student-service.request confirm-payment no-op id={requestId} status={entity.CurrentStatus}",
                nameof(StudentServiceRequestService),
                context: null,
                metadata: new Dictionary<string, object>
                {
                    ["RequestId"] = requestId,
                    ["CurrentStatus"] = entity.CurrentStatus.ToString(),
                });
            return;
        }

        // Resolve the configured automatic transition out of WaitingPayment.
        // If no workflow is attached, default to UnderReview — the canonical
        // next state.
        var nextStatus = await ResolveAutomaticNextStatusAsync(entity, ServiceRequestStatus.WaitingPayment, cancellationToken)
                        ?? ServiceRequestStatus.UnderReview;

        entity.CurrentStatus = nextStatus;
        entity.UpdatedAt = DateTime.UtcNow;
        _requests.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(requestId), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);

        await _logger.LogInfoAsync(
            $"student-service.request payment confirmed id={requestId} → {nextStatus}",
            nameof(StudentServiceRequestService),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
                ["NextStatus"] = nextStatus.ToString(),
            });
    }

    private async Task<StudentServiceRequest> LoadForStudentAsync(Guid studentId, Guid requestId, CancellationToken cancellationToken)
    {
        var entity = await _requests.GetByIdAsync(requestId, includeChildren: false, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.StudentServices.RequestNotFound);

        if (entity.StudentId != studentId)
        {
            // Same existence-leak rule the rest of the project follows.
            throw new NotFoundException(LocalizedKeys.StudentServices.RequestNotFound);
        }

        if (!await _scope.CanAccessStudentAsync(entity.StudentId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.StudentServices.RequestNotFound);
        }

        return entity;
    }

    private async Task<StudentServiceRequest> LoadForStaffAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var entity = await _requests.GetByIdAsync(requestId, includeChildren: false, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.StudentServices.RequestNotFound);

        if (!await _scope.CanAccessStudentAsync(entity.StudentId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.StudentServices.RequestNotFound);
        }

        return entity;
    }

    private async Task<IReadOnlyList<StudentServiceRequest>> FilterByScopeAsync(IReadOnlyList<StudentServiceRequest> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return items;
        var visible = new List<StudentServiceRequest>(items.Count);
        foreach (var r in items)
        {
            if (await _scope.CanAccessStudentAsync(r.StudentId, cancellationToken))
            {
                visible.Add(r);
            }
        }
        return visible;
    }

    private async Task<ServiceRequestStatus> ResolveInitialStatusAsync(StudentService service, CancellationToken cancellationToken)
    {
        if (service.WorkflowDefinitionId.HasValue)
        {
            var initial = await _workflows.ResolveInitialStateAsync(service.WorkflowDefinitionId.Value, cancellationToken);
            if (initial is not null) return initial.Status;
        }
        return ServiceRequestStatus.Submitted;
    }

    private async Task EnsureTransitionAllowedAsync(StudentServiceRequest entity, ServiceRequestStatus toStatus, WorkflowTransitionType expectedType, CancellationToken cancellationToken)
    {
        if (entity.CurrentStatus == toStatus) return; // no-op transition

        // Resolve through the workflow when one is configured; otherwise fall
        // back to the universal status graph below.
        var service = await _services.GetByIdAsync(entity.StudentServiceId, includeChildren: false, cancellationToken);
        if (service?.WorkflowDefinitionId is { } workflowId)
        {
            var transition = await _workflows.ResolveTransitionAsync(workflowId, entity.CurrentStatus, toStatus, cancellationToken);
            if (transition is null)
            {
                throw new ConflictException(LocalizedKeys.StudentServices.InvalidTransition);
            }
            // Manual transitions are gated by the controller's [HasPermission]
            // attribute; the action verb travels along on the row so the
            // catalog stays declarative.
            return;
        }

        // No workflow attached — apply the default graph. Mirrors what an
        // operator would have wired manually in the default workflow.
        var allowed = (entity.CurrentStatus, toStatus) switch
        {
            (ServiceRequestStatus.Submitted, ServiceRequestStatus.UnderReview) => true,
            (ServiceRequestStatus.Submitted, ServiceRequestStatus.Rejected)    => true,
            (ServiceRequestStatus.UnderReview, ServiceRequestStatus.Approved)  => true,
            (ServiceRequestStatus.UnderReview, ServiceRequestStatus.Rejected)  => true,
            (ServiceRequestStatus.Approved, ServiceRequestStatus.Completed)    => true,
            (ServiceRequestStatus.WaitingPayment, ServiceRequestStatus.UnderReview) => expectedType == WorkflowTransitionType.Automatic,
            _ => false,
        };
        if (!allowed)
        {
            throw new ConflictException(LocalizedKeys.StudentServices.InvalidTransition);
        }
    }

    private async Task<ServiceRequestStatus?> ResolveAutomaticNextStatusAsync(StudentServiceRequest entity, ServiceRequestStatus fromStatus, CancellationToken cancellationToken)
    {
        var service = await _services.GetByIdAsync(entity.StudentServiceId, includeChildren: false, cancellationToken);
        if (service?.WorkflowDefinitionId is not { } workflowId) return null;

        var workflow = await _workflows.GetByIdAsync(workflowId, cancellationToken);
        var automatic = workflow?.Transitions
            .FirstOrDefault(t => t.FromStatus == fromStatus && t.TransitionType == WorkflowTransitionType.Automatic);
        return automatic?.ToStatus;
    }

    private void ValidateDynamicFieldsAndDocuments(StudentService service, SubmitStudentServiceRequestRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        // Required-field check — every defined required field must appear in
        // the submission. Duplicate submissions of the same field id reject
        // up front so the unique index on (RequestId, FieldId) does not
        // surface a 500.
        var submittedByField = new Dictionary<Guid, ServiceFieldValueInput>();
        foreach (var v in request.FieldValues)
        {
            if (!submittedByField.TryAdd(v.FieldDefinitionId, v))
            {
                AddError(errors, $"FieldValues[{v.FieldDefinitionId}]", LocalizedKeys.StudentServices.DuplicateFieldValue);
            }
        }

        foreach (var field in service.Fields)
        {
            submittedByField.TryGetValue(field.Id, out var submitted);
            var value = submitted?.Value ?? string.Empty;

            if (field.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                AddError(errors, $"FieldValues[{field.Name}]", LocalizedKeys.StudentServices.RequiredFieldMissing);
                continue;
            }
            if (string.IsNullOrEmpty(value)) continue; // optional + not submitted

            ValidateFieldValue(field, value, errors);
        }

        // Required-document check + per-document constraints.
        var submittedDocs = request.Files
            .GroupBy(f => f.DocumentDefinitionId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var doc in service.Documents)
        {
            submittedDocs.TryGetValue(doc.Id, out var submitted);

            if (doc.IsRequired && submitted is null)
            {
                AddError(errors, $"Files[{doc.Name}]", LocalizedKeys.StudentServices.RequiredDocumentMissing);
                continue;
            }
            if (submitted is null) continue;

            if (doc.MaxFileSizeBytes > 0 && submitted.FileSize > doc.MaxFileSizeBytes)
            {
                AddError(errors, $"Files[{doc.Name}].FileSize", LocalizedKeys.StudentServices.FileTooLarge);
            }
            if (!string.IsNullOrEmpty(doc.AllowedExtensions))
            {
                var allowed = doc.AllowedExtensions
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(e => e.ToLowerInvariant());
                var ext = Path.GetExtension(submitted.FileName).TrimStart('.').ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    AddError(errors, $"Files[{doc.Name}].FileName", LocalizedKeys.StudentServices.InvalidFileExtension);
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static void ValidateFieldValue(ServiceFieldDefinition field, string value, Dictionary<string, string[]> errors)
    {
        var key = $"FieldValues[{field.Name}]";
        switch (field.FieldType)
        {
            case DynamicFieldType.Text:
            case DynamicFieldType.MultilineText:
                if (field.MinLength.HasValue && value.Length < field.MinLength.Value)
                    AddError(errors, key, LocalizedKeys.StudentServices.InvalidFieldValue);
                if (field.MaxLength.HasValue && value.Length > field.MaxLength.Value)
                    AddError(errors, key, LocalizedKeys.StudentServices.InvalidFieldValue);
                break;

            case DynamicFieldType.Number:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
                {
                    AddError(errors, key, LocalizedKeys.StudentServices.InvalidFieldValue);
                    break;
                }
                if (field.MinValue.HasValue && n < field.MinValue.Value)
                    AddError(errors, key, LocalizedKeys.StudentServices.InvalidFieldValue);
                if (field.MaxValue.HasValue && n > field.MaxValue.Value)
                    AddError(errors, key, LocalizedKeys.StudentServices.InvalidFieldValue);
                break;

            case DynamicFieldType.Date:
                // ISO-8601 — the API contract documents this format for date
                // fields, and parsing through CultureInfo.InvariantCulture is
                // the safest cross-locale choice.
                if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                    AddError(errors, key, LocalizedKeys.StudentServices.InvalidFieldValue);
                break;

            case DynamicFieldType.Boolean:
                if (!bool.TryParse(value, out _))
                    AddError(errors, key, LocalizedKeys.StudentServices.InvalidFieldValue);
                break;

            case DynamicFieldType.Dropdown:
                var allowed = (field.DropdownValues ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (allowed.Length == 0 || !allowed.Contains(value, StringComparer.Ordinal))
                    AddError(errors, key, LocalizedKeys.StudentServices.InvalidDropdownValue);
                break;

            case DynamicFieldType.File:
                // File-typed dynamic fields carry their value as the storage
                // path; the per-field shape is validated alongside required
                // file uploads at the document layer.
                break;
        }
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        if (errors.TryGetValue(key, out var existing))
        {
            errors[key] = existing.Append(message).ToArray();
        }
        else
        {
            errors[key] = new[] { message };
        }
    }

    private Task NotifyStudentAsync(StudentServiceRequest entity, string title, string message, NotificationType type, CancellationToken cancellationToken) =>
        _notifications.EnqueueNotificationAsync(entity.StudentId, title, message, type, cancellationToken);

    private static StudentServiceRequestResponse MapToResponse(StudentServiceRequest r) => new()
    {
        Id = r.Id,
        StudentId = r.StudentId,
        StudentServiceId = r.StudentServiceId,
        ServiceCode = r.StudentService?.Code ?? string.Empty,
        ServiceName = r.StudentService?.Name ?? string.Empty,
        CurrentStatus = r.CurrentStatus,
        SubmittedAt = r.SubmittedAt,
        ProcessedAt = r.ProcessedAt,
        AssignedStaffId = r.AssignedStaffId,
        CancellationReason = r.CancellationReason,
        RejectionReason = r.RejectionReason,
        PaymentReferenceId = r.PaymentReferenceId,
        FieldValues = r.FieldValues.Select(v => new ServiceFieldValueResponse
        {
            FieldDefinitionId = v.FieldDefinitionId,
            FieldName = v.FieldDefinition?.Name ?? string.Empty,
            FieldType = v.FieldDefinition?.FieldType ?? DynamicFieldType.Text,
            Value = v.Value,
        }).ToList(),
        Documents = r.Documents.Select(d => new ServiceDocumentSubmissionResponse
        {
            Id = d.Id,
            DocumentDefinitionId = d.DocumentDefinitionId,
            DocumentName = d.DocumentDefinition?.Name ?? string.Empty,
            FileName = d.FileName,
            ContentType = d.ContentType,
            FileSize = d.FileSize,
            UploadedAt = d.CreatedAt,
        }).ToList(),
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };

    private static StudentServiceRequestSummaryResponse MapToSummary(StudentServiceRequest r) => new()
    {
        Id = r.Id,
        StudentId = r.StudentId,
        StudentServiceId = r.StudentServiceId,
        ServiceCode = r.StudentService?.Code ?? string.Empty,
        ServiceName = r.StudentService?.Name ?? string.Empty,
        CurrentStatus = r.CurrentStatus,
        SubmittedAt = r.SubmittedAt,
        AssignedStaffId = r.AssignedStaffId,
        PaymentReferenceId = r.PaymentReferenceId,
        CreatedAt = r.CreatedAt,
    };

    private static ValidationException ValidationFrom(FluentValidation.Results.ValidationResult result) =>
        new(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}

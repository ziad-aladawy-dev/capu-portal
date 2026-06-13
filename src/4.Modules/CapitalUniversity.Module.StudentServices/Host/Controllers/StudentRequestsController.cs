using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.Module.StudentServices.Host.Controllers;

[ApiController]
[Route("api/student-services/requests")]
[Authorize]
public class StudentRequestsController : ControllerBase
{
    private readonly IStudentRequestService _requestService;
    private readonly ICurrentUser _currentUser;
    private readonly IUserScope _userScope;
    private readonly IPermissionManagementService _permissions;
    private readonly IStudentRequestRepository _requestRepository;

    public StudentRequestsController(
        IStudentRequestService requestService,
        ICurrentUser currentUser,
        IUserScope userScope,
        IPermissionManagementService permissions,
        IStudentRequestRepository requestRepository)
    {
        _requestService = requestService;
        _currentUser = currentUser;
        _userScope = userScope;
        _permissions = permissions;
        _requestRepository = requestRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyRequests(CancellationToken cancellationToken)
    {
        var result = await _requestService.GetStudentRequestsAsync(_currentUser.Id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("all")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetAllRequests(CancellationToken cancellationToken)
    {
        var result = await _requestService.GetAllRequestsForStaffAsync(cancellationToken);
        return Ok(result);
    }

    // Mixed access: a student may view THEIR OWN request; staff need RequestsView.
    // Students hold no permission grants by design (context-scoped model), so the
    // role branch happens here instead of a [HasPermission] attribute.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (_userScope.IsStudent)
        {
            // 404 (not 403) for non-owned ids — no existence leak, matches the
            // self-access semantics of the other student-facing controllers.
            if (!await StudentOwnsRequestAsync(id, cancellationToken)) return NotFound();
        }
        else if (!await StaffHasPermissionAsync(PermissionNames.StudentServices.RequestsView, cancellationToken))
        {
            return Forbid();
        }

        var result = await _requestService.GetStudentRequestAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDraft([FromQuery] Guid serviceId, CancellationToken cancellationToken)
    {
        var result = await _requestService.CreateDraftAsync(_currentUser.Id, serviceId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/step")]
    public async Task<IActionResult> SaveStep(Guid id, [FromBody] SubmitStepDataDto request, CancellationToken cancellationToken)
    {
        var result = await _requestService.SaveStepDataAsync(id, request.StepKey, request.Data, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _requestService.SubmitRequestAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/payment")]
    public async Task<IActionResult> ProcessPayment(Guid id, [FromBody] PaymentRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _requestService.ProcessPaymentAsync(id, request.PaymentMethod, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsAssign)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRequest request, CancellationToken cancellationToken)
    {
        var result = await _requestService.AssignToStaffAsync(id, request.StaffId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsEditClose)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _requestService.UpdateStatusAsync(id, request.NewStatus, request.Comment, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/comment")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsEditClose)]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentRequest request, CancellationToken cancellationToken)
    {
        var result = await _requestService.AddCommentAsync(id, request.Comment, _currentUser.Id, _currentUser.Role, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/close-record")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsEditClose)]
    public async Task<IActionResult> CloseRecord(Guid id, CancellationToken cancellationToken)
    {
        var result = await _requestService.CloseRequestAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/open-record")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsOpen)]
    public async Task<IActionResult> OpenRecord(Guid id, CancellationToken cancellationToken)
    {
        var result = await _requestService.OpenRequestAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("assigned-to-me")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetAssignedToMe(CancellationToken cancellationToken)
    {
        var result = await _requestService.GetPendingAssignmentsAsync(_currentUser.Id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("assigned-to-staff/{staffId:guid}")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetAssignedToStaff(Guid staffId, CancellationToken cancellationToken)
    {
        var result = await _requestService.GetAssignedToStaffAsync(staffId, cancellationToken);
        return Ok(new { items = result, totalCount = result.Count });
    }

    [HttpGet("by-student/{studentId:guid}")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetByStudentId(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _requestService.GetStudentRequestsAsync(studentId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("staff/paged")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetPagedForStaff(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool ascending = false,
        CancellationToken cancellationToken = default)
    {
        Guid? staffId = null;
        if (!_userScope.IsStudent)
        {
            var canBypass = _userScope.HasGlobalScope ||
                await StaffHasPermissionAsync(PermissionNames.StudentServices.RequestsAssign, cancellationToken);
            staffId = canBypass ? null : _currentUser.Id;
        }
        var result = await _requestService.GetPagedRequestsForStaffAsync(page, pageSize, search, sortBy, ascending, staffId, cancellationToken);
        return Ok(result);
    }

    // Mixed access: student → own request only; staff → RequestsView.
    [HttpGet("{id:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid id, CancellationToken cancellationToken)
    {
        if (_userScope.IsStudent)
        {
            if (!await StudentOwnsRequestAsync(id, cancellationToken)) return NotFound();
        }
        else if (!await StaffHasPermissionAsync(PermissionNames.StudentServices.RequestsView, cancellationToken))
        {
            return Forbid();
        }

        var attachments = await _requestService.GetAttachmentsByRequestIdAsync(id, cancellationToken);
        return Ok(attachments);
    }

    [HttpGet("pending/{serviceId:guid}")]
    public async Task<IActionResult> GetPendingRequest(Guid serviceId, CancellationToken cancellationToken)
    {
        var studentId = _currentUser.Id;
        var request = await _requestService.GetPendingRequestForStudentAndServiceAsync(studentId, serviceId, cancellationToken);
        return Ok(request);
    }

    private async Task<bool> StudentOwnsRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await _requestRepository.GetByIdAsync(requestId, cancellationToken);
        return request != null && request.StudentId == _currentUser.Id;
    }

    // Imperative version of the [HasPermission] check (same lookup the
    // PermissionHandler uses). Identities MUST go through PermissionIdentity.Parse —
    // GetPermissionLookupAsync stores canonical (Create-normalized) entries, so the
    // raw kebab-case constants would never match.
    private async Task<bool> StaffHasPermissionAsync(string permission, CancellationToken cancellationToken)
    {
        var grants = await _permissions.GetPermissionLookupAsync(_currentUser.Id, cancellationToken);
        return grants.Contains(PermissionIdentity.Parse(permission));
    }

    // Helper records
    public record SubmitStepDataDto(string StepKey, object Data);
    public record PaymentRequestDto(string PaymentMethod);
    public record AssignRequest(Guid StaffId);
    public record UpdateStatusRequest(RequestStatus NewStatus, string? Comment);
    public record AddCommentRequest(string Comment);
}
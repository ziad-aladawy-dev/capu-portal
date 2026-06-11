using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
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

    public StudentRequestsController(IStudentRequestService requestService, ICurrentUser currentUser)
    {
        _requestService = requestService;
        _currentUser = currentUser;
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

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
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

    [HttpGet("assigned-to-me")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetAssignedToMe(CancellationToken cancellationToken)
    {
        var result = await _requestService.GetPendingAssignmentsAsync(_currentUser.Id, cancellationToken);
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
        var result = await _requestService.GetPagedRequestsForStaffAsync(page, pageSize, search, sortBy, ascending, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/attachments")]
    [Authorize(Policy = "Permission:" + PermissionNames.StudentServices.RequestsView)]
    public async Task<IActionResult> GetAttachments(Guid id, CancellationToken cancellationToken)
    {
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

    // Helper records
    public record SubmitStepDataDto(string StepKey, object Data);
    public record PaymentRequestDto(string PaymentMethod);
    public record AssignRequest(Guid StaffId);
    public record UpdateStatusRequest(RequestStatus NewStatus, string? Comment);
    public record AddCommentRequest(string Comment);
}
//using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _requestService.GetRequestAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDraft([FromQuery] Guid serviceId, CancellationToken cancellationToken)
    {
        var result = await _requestService.CreateDraftAsync(_currentUser.Id, serviceId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/save-step")]
    public async Task<IActionResult> SaveStep(Guid id, [FromBody] SaveStepRequest request, CancellationToken cancellationToken)
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

    [HttpPost("{id:guid}/assign")]
    //[HasPermission("student-services.requests.Assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRequest request, CancellationToken cancellationToken)
    {
        var result = await _requestService.AssignToStaffAsync(id, request.StaffId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/status")]
    //[HasPermission("student-services.requests.Manage")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _requestService.UpdateStatusAsync(id, request.NewStatus, request.Comment, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/comment")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentRequest request, CancellationToken cancellationToken)
    {
        var result = await _requestService.AddCommentAsync(id, request.Comment, _currentUser.Id, _currentUser.Role, cancellationToken);
        return Ok(result);
    }

    [HttpGet("assigned-to-me")]
    //[HasPermission("student-services.requests.ViewAssigned")]
    public async Task<IActionResult> GetAssignedToMe(CancellationToken cancellationToken)
    {
        var result = await _requestService.GetPendingAssignmentsAsync(_currentUser.Id, cancellationToken);
        return Ok(result);
    }

    public record SaveStepRequest(string StepKey, object Data);
    public record AssignRequest(Guid StaffId);
    public record UpdateStatusRequest(RequestStatus NewStatus, string? Comment);
    public record AddCommentRequest(string Comment);
}
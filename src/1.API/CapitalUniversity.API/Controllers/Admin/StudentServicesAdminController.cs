using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers.Admin;

/// <summary>
/// Admin-facing service catalog endpoints. Gated by the
/// <c>student-services.services</c> resource permissions; only staff with
/// the corresponding grants reach these.
/// </summary>
[ApiController]
[Route("api/admin/student-services")]
public class StudentServicesAdminController : ControllerBase
{
    private readonly IStudentServiceService _service;
    private readonly IWorkflowService _workflows;

    public StudentServicesAdminController(IStudentServiceService service, IWorkflowService workflows)
    {
        _service = service;
        _workflows = workflows;
    }

    [HttpGet]
    [HasPermission(PermissionNames.StudentServicesCatalog.View)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, isActive, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.StudentServicesCatalog.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(PermissionNames.StudentServicesCatalog.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateStudentServiceRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Service created successfully" });
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionNames.StudentServicesCatalog.EditClose)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentServiceRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(new { Message = "Service updated successfully" });
    }

    [HttpPost("{id:guid}/toggle-status")]
    [HasPermission(PermissionNames.StudentServicesCatalog.EditClose)]
    public async Task<IActionResult> ToggleStatus(Guid id, [FromBody] ToggleStudentServiceStatusRequest request, CancellationToken cancellationToken)
    {
        await _service.ToggleStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(new { Message = "Service status updated" });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionNames.StudentServicesCatalog.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(new { Message = "Service deleted" });
    }

    // Workflow management lives under the same admin surface — admins
    // configuring services need to configure their workflows.
    [HttpGet("workflows")]
    [HasPermission(PermissionNames.StudentServiceWorkflows.View)]
    public async Task<IActionResult> ListWorkflows(CancellationToken cancellationToken)
    {
        var result = await _workflows.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("workflows/{id:guid}")]
    [HasPermission(PermissionNames.StudentServiceWorkflows.View)]
    public async Task<IActionResult> GetWorkflow(Guid id, CancellationToken cancellationToken)
    {
        var workflow = await _workflows.GetByIdAsync(id, cancellationToken);
        if (workflow is null) return NotFound();
        return Ok(workflow);
    }

    [HttpPost("workflows")]
    [HasPermission(PermissionNames.StudentServiceWorkflows.Insert)]
    public async Task<IActionResult> CreateWorkflow([FromBody] CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken)
    {
        var id = await _workflows.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetWorkflow), new { id }, new { id, Message = "Workflow created successfully" });
    }

    [HttpDelete("workflows/{id:guid}")]
    [HasPermission(PermissionNames.StudentServiceWorkflows.Delete)]
    public async Task<IActionResult> DeleteWorkflow(Guid id, CancellationToken cancellationToken)
    {
        await _workflows.DeleteAsync(id, cancellationToken);
        return Ok(new { Message = "Workflow deleted" });
    }
}

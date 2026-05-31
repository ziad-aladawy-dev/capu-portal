//using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

namespace CapitalUniversity.Module.StudentServices.Host.Controllers;

[ApiController]
[Route("api/student-services/workflows")]
[Authorize]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowManagementService _service;

    public WorkflowsController(IWorkflowManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    //[HasPermission("student-services.workflows.View")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllWorkflowsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    //[HasPermission("student-services.workflows.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetWorkflowAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    //[HasPermission("student-services.workflows.Insert")]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDto dto, CancellationToken cancellationToken)
    {
        var id = await _service.CreateWorkflowAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    //[HasPermission("student-services.workflows.EditClose")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowDto dto, CancellationToken cancellationToken)
    {
        await _service.UpdateWorkflowAsync(id, dto, cancellationToken);
        return Ok(new { message = "Workflow updated" });
    }

    [HttpDelete("{id:guid}")]
    //[HasPermission("student-services.workflows.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteWorkflowAsync(id, cancellationToken);
        return Ok(new { message = "Workflow deleted" });
    }

    [HttpPost("{workflowId:guid}/steps")]
    //[HasPermission("student-services.workflows.EditClose")]
    public async Task<IActionResult> AddStep(Guid workflowId, [FromBody] CreateWorkflowStepDto dto, CancellationToken cancellationToken)
    {
        var id = await _service.AddStepAsync(workflowId, dto, cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("steps/{stepId:guid}")]
    //[HasPermission("student-services.workflows.EditClose")]
    public async Task<IActionResult> UpdateStep(Guid stepId, [FromBody] UpdateWorkflowStepDto dto, CancellationToken cancellationToken)
    {
        await _service.UpdateStepAsync(stepId, dto, cancellationToken);
        return Ok(new { message = "Step updated" });
    }

    [HttpDelete("steps/{stepId:guid}")]
    //[HasPermission("student-services.workflows.EditClose")]
    public async Task<IActionResult> DeleteStep(Guid stepId, CancellationToken cancellationToken)
    {
        await _service.DeleteStepAsync(stepId, cancellationToken);
        return Ok(new { message = "Step deleted" });
    }
}
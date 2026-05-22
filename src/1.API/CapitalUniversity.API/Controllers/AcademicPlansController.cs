using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/academic-plans")]
public class AcademicPlansController : ControllerBase
{
    private readonly IAcademicPlanService _service;

    public AcademicPlansController(IAcademicPlanService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.AcademicPlans.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("by-structure/{structureNodeId:guid}")]
    [HasPermission(PermissionNames.AcademicPlans.View)]
    public async Task<IActionResult> GetForStructureNode(Guid structureNodeId, CancellationToken cancellationToken)
    {
        var result = await _service.GetForStructureNodeAsync(structureNodeId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(PermissionNames.AcademicPlans.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateAcademicPlanRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Academic plan created successfully" });
    }

    [HttpPatch("{id:guid}")]
    [HasPermission(PermissionNames.AcademicPlans.EditClose)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAcademicPlanRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(new { Message = "Academic plan updated successfully" });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionNames.AcademicPlans.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(new { Message = "Academic plan deleted successfully" });
    }

    [HttpPost("{id:guid}/courses")]
    [HasPermission(PermissionNames.AcademicPlans.EditClose)]
    public async Task<IActionResult> AddCourse(Guid id, [FromBody] AddPlanCourseRequest request, CancellationToken cancellationToken)
    {
        var planCourseId = await _service.AddCourseAsync(id, request, cancellationToken);
        return Ok(new { id = planCourseId, Message = "Course added to academic plan" });
    }

    [HttpDelete("{id:guid}/courses/{planCourseId:guid}")]
    [HasPermission(PermissionNames.AcademicPlans.EditClose)]
    public async Task<IActionResult> RemoveCourse(Guid id, Guid planCourseId, CancellationToken cancellationToken)
    {
        await _service.RemoveCourseAsync(id, planCourseId, cancellationToken);
        return Ok(new { Message = "Course removed from academic plan" });
    }

    [HttpPost("{id:guid}/close-record")]
    [HasPermission(PermissionNames.AcademicPlans.EditClose)]
    public async Task<IActionResult> CloseRecord(Guid id, CancellationToken cancellationToken)
    {
        await _service.CloseRecordAsync(id, cancellationToken);
        return Ok(new { Message = "Academic plan closed" });
    }

    [HttpPost("{id:guid}/open-record")]
    [HasPermission(PermissionNames.AcademicPlans.Open)]
    public async Task<IActionResult> OpenRecord(Guid id, CancellationToken cancellationToken)
    {
        await _service.OpenRecordAsync(id, cancellationToken);
        return Ok(new { Message = "Academic plan reopened" });
    }
}

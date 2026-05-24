using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
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

    /// <summary>
    /// Paged plan search. Filters: <c>structureNodeId</c>, <c>isActive</c>,
    /// <c>effectiveFromInclusive</c>, <c>effectiveToExclusive</c>; free-text on name.
    /// Sort: <c>name|effectiveFrom|createdAt</c>.
    /// </summary>
    [HttpGet("search")]
    [HasPermission(PermissionNames.AcademicPlans.View)]
    public async Task<IActionResult> Search([FromQuery] AcademicPlanSearchQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(query, cancellationToken);
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

    /// <summary>
    /// Atomic add/remove diff on the plan's composition. Either every step in
    /// the diff lands or none do — a curriculum revision should never leave
    /// the plan half-applied. Per-step errors surface as 4xx (validation / not
    /// found / conflict) and the entire batch is rejected.
    /// </summary>
    [HttpPost("{id:guid}/courses/batch")]
    [HasPermission(PermissionNames.AcademicPlans.EditClose)]
    public async Task<IActionResult> BatchUpdateCourses(Guid id, [FromBody] BatchPlanCoursesRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { Message = "A batch request body is required." });
        }
        var total = request.Add.Count + request.Remove.Count;
        if (total > BulkConstants.MaxBulkSize)
        {
            return BadRequest(new { Message = $"Cannot apply more than {BulkConstants.MaxBulkSize} composition changes in one batch." });
        }

        await _service.BatchUpdateCoursesAsync(id, request, cancellationToken);
        return Ok(new { Message = "Academic plan composition updated" });
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

    /// <summary>3.9 — bulk delete academic plans.</summary>
    [HttpPost("delete")]
    [HasPermission(PermissionNames.AcademicPlans.Delete)]
    public async Task<IActionResult> BulkDelete([FromBody] BulkActionRequest request, CancellationToken cancellationToken)
    {
        if (request?.Ids is null || request.Ids.Count == 0)
            return BadRequest(new { Message = "At least one id is required." });
        if (request.Ids.Count > BulkConstants.MaxBulkSize)
            return BadRequest(new { Message = $"Cannot delete more than {BulkConstants.MaxBulkSize} plans in one request." });

        var result = await _service.DeleteManyAsync(request.Ids, cancellationToken);
        return Ok(result);
    }
}

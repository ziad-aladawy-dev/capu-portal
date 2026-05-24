using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/course-offerings")]
public class CourseOfferingsController : ControllerBase
{
    private readonly ICourseOfferingService _service;

    public CourseOfferingsController(ICourseOfferingService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.CourseOfferings.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet]
    [HasPermission(PermissionNames.CourseOfferings.View)]
    public async Task<IActionResult> GetForNodeSemester(
        [FromQuery] Guid structureNodeId,
        [FromQuery] Guid semesterId,
        [FromQuery] OfferingStatus? status,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetForNodeSemesterAsync(structureNodeId, semesterId, status, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-course")]
    [HasPermission(PermissionNames.CourseOfferings.View)]
    public async Task<IActionResult> GetForCourse(
        [FromQuery] Guid courseId,
        [FromQuery] Guid semesterId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetForCourseAsync(courseId, semesterId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(PermissionNames.CourseOfferings.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateCourseOfferingRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Course offering created successfully" });
    }

    [HttpPatch("{id:guid}")]
    [HasPermission(PermissionNames.CourseOfferings.EditClose)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseOfferingRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(new { Message = "Course offering updated successfully" });
    }
}

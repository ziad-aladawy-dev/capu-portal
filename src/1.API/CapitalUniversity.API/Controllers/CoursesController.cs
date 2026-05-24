using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _service;

    public CoursesController(ICourseService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(PermissionNames.Courses.View)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await _service.GetActiveAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Paged catalog search. Filters: <c>category</c>, <c>isActive</c>, credit
    /// range; free-text <c>search</c> matches code or title.
    /// Sort: <c>code|creditHours|createdAt</c>.
    /// </summary>
    [HttpGet("search")]
    [HasPermission(PermissionNames.Courses.View)]
    public async Task<IActionResult> Search([FromQuery] CourseSearchQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.Courses.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(PermissionNames.Courses.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Course created successfully" });
    }

    [HttpPatch("{id:guid}")]
    [HasPermission(PermissionNames.Courses.EditClose)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(new { Message = "Course updated successfully" });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionNames.Courses.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(new { Message = "Course deleted successfully" });
    }

    [HttpPost("{id:guid}/close-record")]
    [HasPermission(PermissionNames.Courses.EditClose)]
    public async Task<IActionResult> CloseRecord(Guid id, CancellationToken cancellationToken)
    {
        await _service.CloseRecordAsync(id, cancellationToken);
        return Ok(new { Message = "Course closed" });
    }

    [HttpPost("{id:guid}/open-record")]
    [HasPermission(PermissionNames.Courses.Open)]
    public async Task<IActionResult> OpenRecord(Guid id, CancellationToken cancellationToken)
    {
        await _service.OpenRecordAsync(id, cancellationToken);
        return Ok(new { Message = "Course reopened" });
    }
}

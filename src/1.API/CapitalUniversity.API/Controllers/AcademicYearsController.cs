using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Semesters;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/academic-years")]
public class AcademicYearsController : ControllerBase
{
    private readonly IAcademicYearService _service;

    public AcademicYearsController(IAcademicYearService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(PermissionNames.AcademicTimeline.View)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.AcademicTimeline.View)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(PermissionNames.AcademicTimeline.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateAcademicYearRequest request)
    {
        var id = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Academic year created successfully" });
    }

    [HttpPatch("{id:guid}")]
    [HasPermission(PermissionNames.AcademicTimeline.EditClose)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAcademicYearRequest request)
    {
        await _service.UpdateAsync(id, request);
        return Ok(new { Message = "Academic year updated successfully" });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionNames.AcademicTimeline.Delete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return Ok(new { Message = "Academic year deleted successfully" });
    }

    [HttpGet("{id:guid}/semesters")]
    [HasPermission(PermissionNames.AcademicTimeline.View)]
    public async Task<IActionResult> GetSemesters(Guid id, [FromServices] ISemesterService semesterService)
    {
        var result = await semesterService.GetByAcademicYearIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// Recomputes the <c>IsCurrent</c> flag across all academic years against
    /// the server's UTC clock. Exactly one row matching today's date will end
    /// up with <c>IsCurrent = true</c>; all others are cleared. Idempotent —
    /// invoking twice with no intervening date or schema change is a no-op.
    /// The same logic runs on a background timer
    /// (<see cref="Infrastructure.Services.Semesters.AcademicTimelineBackgroundService"/>);
    /// this endpoint is the manual trigger.
    /// </summary>
    /// <remarks>
    /// This is a WRITE operation despite taking no body. Side effects:
    /// <list type="bullet">
    ///   <item><description>UPDATEs <c>AcademicYears.IsCurrent</c> on zero or more rows.</description></item>
    ///   <item><description>UPDATEs <c>AcademicYears.UpdatedAt</c> on every mutated row.</description></item>
    /// </list>
    /// The <c>resolve</c> route is a deprecated alias retained for the frontend
    /// and the background service; new callers should use <c>recompute-current</c>.
    /// </remarks>
    [HttpPost("recompute-current")]
    [HttpPost("resolve")]
    [HasPermission(PermissionNames.AcademicTimeline.EditClose)]
    public async Task<IActionResult> RecomputeCurrent()
    {
        await _service.ResolveCurrentYearAsync();
        return Ok(new { Message = "Current academic year flag recomputed" });
    }

    [HttpPost("{id:guid}/close-record")]
    [HasPermission(PermissionNames.AcademicTimeline.EditClose)]
    public async Task<IActionResult> CloseRecord(Guid id)
    {
        await _service.CloseRecordAsync(id);
        return Ok(new { Message = "Academic year closed" });
    }

    [HttpPost("{id:guid}/open-record")]
    [HasPermission(PermissionNames.AcademicTimeline.Open)]
    public async Task<IActionResult> OpenRecord(Guid id)
    {
        await _service.OpenRecordAsync(id);
        return Ok(new { Message = "Academic year reopened" });
    }
}

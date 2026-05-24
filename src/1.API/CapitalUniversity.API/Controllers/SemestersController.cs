using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Semesters;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/semesters")]
public class SemestersController : ControllerBase
{
    private readonly ISemesterService _service;

    public SemestersController(ISemesterService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.AcademicTimeline.View)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("current")]
    [HasPermission(PermissionNames.AcademicTimeline.View)]
    public async Task<IActionResult> GetCurrent()
    {
        var result = await _service.GetCurrentAsync();
        if (result == null) return NotFound("No current semester found.");
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(PermissionNames.AcademicTimeline.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateSemesterRequest request)
    {
        var id = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Semester created successfully" });
    }

    [HttpPatch("{id:guid}")]
    [HasPermission(PermissionNames.AcademicTimeline.EditClose)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSemesterRequest request)
    {
        await _service.UpdateAsync(id, request);
        return Ok(new { Message = "Semester updated successfully" });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionNames.AcademicTimeline.Delete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return Ok(new { Message = "Semester deleted successfully" });
    }

    /// <summary>
    /// Recomputes the <c>IsCurrent</c> flag across all semesters in the
    /// current academic year against the server's UTC clock. At most one
    /// semester per academic year ends up with <c>IsCurrent = true</c>;
    /// when no academic year is current, all semester flags are cleared.
    /// Idempotent — invoking twice with no intervening date or schema change
    /// is a no-op. The same logic runs on a background timer
    /// (<see cref="Infrastructure.Services.Semesters.AcademicTimelineBackgroundService"/>);
    /// this endpoint is the manual trigger.
    /// </summary>
    /// <remarks>
    /// This is a WRITE operation despite taking no body. Side effects:
    /// <list type="bullet">
    ///   <item><description>UPDATEs <c>Semesters.IsCurrent</c> on zero or more rows.</description></item>
    ///   <item><description>UPDATEs <c>Semesters.UpdatedAt</c> on every mutated row.</description></item>
    /// </list>
    /// The <c>resolve</c> route is a deprecated alias retained for the frontend
    /// and the background service; new callers should use <c>recompute-current</c>.
    /// </remarks>
    [HttpPost("recompute-current")]
    [HttpPost("resolve")]
    [HasPermission(PermissionNames.AcademicTimeline.EditClose)]
    public async Task<IActionResult> RecomputeCurrent()
    {
        await _service.ResolveCurrentSemesterAsync();
        return Ok(new { Message = "Current semester flag recomputed" });
    }

    [HttpPost("{id:guid}/close-record")]
    [HasPermission(PermissionNames.AcademicTimeline.EditClose)]
    public async Task<IActionResult> CloseRecord(Guid id)
    {
        await _service.CloseRecordAsync(id);
        return Ok(new { Message = "Semester closed" });
    }

    [HttpPost("{id:guid}/open-record")]
    [HasPermission(PermissionNames.AcademicTimeline.Open)]
    public async Task<IActionResult> OpenRecord(Guid id)
    {
        await _service.OpenRecordAsync(id);
        return Ok(new { Message = "Semester reopened" });
    }
}

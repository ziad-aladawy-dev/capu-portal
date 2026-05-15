using CapitalUniversity.API.Infrastructure;
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
    [HasPermission("Academic.Semester.View")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("current")]
    [HasPermission("Academic.Semester.View")]
    public async Task<IActionResult> GetCurrent()
    {
        var result = await _service.GetCurrentAsync();
        if (result == null) return NotFound("No current semester found.");
        return Ok(result);
    }

    [HttpPost]
    [HasPermission("Academic.Semester.Insert")]
    public async Task<IActionResult> Create([FromBody] CreateSemesterRequest request)
    {
        var id = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Semester created successfully" });
    }

    [HttpPatch("{id:guid}")]
    [HasPermission("Academic.Semester.EditClose")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSemesterRequest request)
    {
        await _service.UpdateAsync(id, request);
        return Ok(new { Message = "Semester updated successfully" });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("Academic.Semester.Delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return Ok(new { Message = "Semester deleted successfully" });
    }

    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve()
    {
        await _service.ResolveCurrentSemesterAsync();
        return Ok(new { Message = "Semester resolution triggered" });
    }
}

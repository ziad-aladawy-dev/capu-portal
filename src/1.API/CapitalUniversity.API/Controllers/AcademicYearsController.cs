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

    [HttpPost("resolve")]
    [HasPermission(PermissionNames.AcademicTimeline.EditClose)]
    public async Task<IActionResult> Resolve()
    {
        await _service.ResolveCurrentYearAsync();
        return Ok(new { Message = "Academic year resolution triggered" });
    }
}

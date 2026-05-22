using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/students/{studentId:guid}/profile-records")]
public class StudentProfileRecordsController : ControllerBase
{
    private readonly IStudentProfileService _service;

    public StudentProfileRecordsController(IStudentProfileService service)
    {
        _service = service;
    }

    [HttpGet]
    [HasPermission(PermissionNames.StudentProfileRecords.View)]
    public async Task<IActionResult> GetForStudent(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _service.GetForStudentAsync(studentId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-category/{category}")]
    [HasPermission(PermissionNames.StudentProfileRecords.View)]
    public async Task<IActionResult> GetByCategory(Guid studentId, StudentProfileCategory category, [FromQuery] string? customKey, CancellationToken cancellationToken)
    {
        var result = await _service.GetForStudentCategoryAsync(studentId, category, customKey, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{recordId:guid}")]
    [HasPermission(PermissionNames.StudentProfileRecords.View)]
    public async Task<IActionResult> GetById(Guid studentId, Guid recordId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(recordId, cancellationToken);
        if (result is null || result.StudentId != studentId) return NotFound();
        return Ok(result);
    }

    [HttpPut]
    [HasPermission(PermissionNames.StudentProfileRecords.Insert)]
    public async Task<IActionResult> Upsert(Guid studentId, [FromBody] UpsertStudentProfileRecordRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.UpsertAsync(studentId, request, cancellationToken);
        return Ok(new { id, Message = "Profile record persisted" });
    }

    [HttpPost("{recordId:guid}/verify")]
    [HasPermission(PermissionNames.StudentProfileRecords.EditClose)]
    public async Task<IActionResult> Verify(Guid studentId, Guid recordId, [FromBody] VerifyStudentProfileRecordRequest request, CancellationToken cancellationToken)
    {
        await _service.VerifyAsync(recordId, request, cancellationToken);
        return Ok(new { Message = "Profile record verified" });
    }

    [HttpDelete("{recordId:guid}")]
    [HasPermission(PermissionNames.StudentProfileRecords.Delete)]
    public async Task<IActionResult> Delete(Guid studentId, Guid recordId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(recordId, cancellationToken);
        return Ok(new { Message = "Profile record deleted" });
    }
}

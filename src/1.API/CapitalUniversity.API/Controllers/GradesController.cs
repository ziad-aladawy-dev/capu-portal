using System.Security.Claims;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.AcademicRecords.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Student-facing read endpoints over academic outcomes synchronized from
/// external academic systems — grade history, per-semester detail, and the
/// academic summary. The caller's <c>studentId</c> is bound from the JWT, so a
/// student can only ever read their own records even if they fabricate a request;
/// the service layer re-checks scope and returns an empty result for anything out
/// of scope (no existence leak). The portal does not enter or modify grades —
/// there are no write verbs here.
/// </summary>
[ApiController]
[Route("api/grades")]
public class GradesController : ControllerBase
{
    private readonly IAcademicRecordsService _service;

    public GradesController(IAcademicRecordsService service)
    {
        _service = service;
    }

    /// <summary>Full grade history for the signed-in student, grouped by term (Latest → Oldest).</summary>
    [HttpGet("history")]
    [HasPermission(PermissionNames.Grades.View)]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
    {
        var result = await _service.GetStudentSemesterHistoryAsync(ResolveStudentId(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Detailed grades for the signed-in student in one academic term.</summary>
    [HttpGet("history/{semesterId:guid}")]
    [HasPermission(PermissionNames.Grades.View)]
    public async Task<IActionResult> GetSemesterGrades(Guid semesterId, CancellationToken cancellationToken)
    {
        var result = await _service.GetSemesterGradesAsync(ResolveStudentId(), semesterId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Synchronized academic summary (GPA / CGPA / credits / standing) for the signed-in student.</summary>
    [HttpGet("summary")]
    [HasPermission(PermissionNames.Grades.View)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var result = await _service.GetAcademicSummaryAsync(ResolveStudentId(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private Guid ResolveStudentId()
    {
        // The Authentication module mints a JWT carrying the user id as the
        // standard NameIdentifier claim — for these student-facing endpoints
        // that is the student id. Matches RegisteredCoursesController.
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(id, out var guid)) return guid;
        throw new UnauthorizedAccessException();
    }
}

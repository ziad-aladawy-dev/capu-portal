using System.Security.Claims;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.Registration.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Student-facing read endpoints over course registrations synchronized from
/// external academic systems. The caller's <c>studentId</c> is bound from the
/// JWT, so a student can only ever read their own registrations even if they
/// fabricate a request; the service layer re-checks scope and returns an empty
/// result for anything out of scope (no existence leak). The portal does not
/// manage registration — there are no write verbs here.
/// </summary>
[ApiController]
[Route("api/courses")]
public class RegisteredCoursesController : ControllerBase
{
    private readonly IStudentRegistrationService _service;

    public RegisteredCoursesController(IStudentRegistrationService service)
    {
        _service = service;
    }

    /// <summary>Current (actively enrolled) registrations for the signed-in student.</summary>
    [HttpGet("registered")]
    [HasPermission(PermissionNames.RegisteredCourses.View)]
    public async Task<IActionResult> GetRegistered(CancellationToken cancellationToken)
    {
        var result = await _service.GetRegisteredCoursesAsync(ResolveStudentId(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Full registration history for the signed-in student, grouped by academic term.</summary>
    [HttpGet("history")]
    [HasPermission(PermissionNames.RegisteredCourses.View)]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
    {
        var result = await _service.GetStudentRegistrationHistoryAsync(ResolveStudentId(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Every attempt the signed-in student made at one specific course, ordered by attempt number.</summary>
    [HttpGet("{courseId:guid}/attempts")]
    [HasPermission(PermissionNames.RegisteredCourses.View)]
    public async Task<IActionResult> GetCourseAttempts(Guid courseId, CancellationToken cancellationToken)
    {
        var result = await _service.GetCourseAttemptsAsync(ResolveStudentId(), courseId, cancellationToken);
        return Ok(result);
    }

    private Guid ResolveStudentId()
    {
        // The Authentication module mints a JWT carrying the user id as the
        // standard NameIdentifier claim — for these student-facing endpoints
        // that is the student id. Matches StudentServiceRequestsController.
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(id, out var guid)) return guid;
        throw new UnauthorizedAccessException();
    }
}

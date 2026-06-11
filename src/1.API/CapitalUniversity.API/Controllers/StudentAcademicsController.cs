using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.AcademicRecords.Abstractions;
using CapitalUniversity.Modules.Registration.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// H10 — Admin / advisor reads of a SPECIFIC student's academic record (the
/// Student Academic Hub), keyed by route <c>studentId</c>. These complement the
/// student-self endpoints (TranscriptController / GradesController /
/// RegisteredCoursesController, which bind the studentId from the JWT): here the
/// id comes from the route and access is scope-guarded via
/// <see cref="PermissionScopeKind.Student"/>, so a caller can only read students
/// their grants cover (<c>IEffectiveScope.CanAccessStudentAsync</c> — a student
/// resolves to self, an advisor/registrar to students under their structure, a
/// global grant to all). The underlying services re-check scope and return
/// null/empty for anything out of scope (no existence leak).
///
/// Routes are nested under api/students/{studentId} and use deeper templates than
/// StudentsController, so the two controllers sharing the prefix do not collide.
/// </summary>
[ApiController]
[Route("api/students")]
public class StudentAcademicsController : ControllerBase
{
    private readonly ITranscriptService _transcript;
    private readonly IAcademicRecordsService _academics;
    private readonly IStudentRegistrationService _registration;

    public StudentAcademicsController(
        ITranscriptService transcript,
        IAcademicRecordsService academics,
        IStudentRegistrationService registration)
    {
        _transcript = transcript;
        _academics = academics;
        _registration = registration;
    }

    /// <summary>The student's requirement-category transcript.</summary>
    [HttpGet("{studentId:guid}/transcript")]
    [HasPermission(PermissionNames.Transcript.View, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> GetTranscript(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _transcript.GetStudentTranscriptAsync(studentId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>The student's synchronized academic summary (GPA / CGPA / credits / standing).</summary>
    [HttpGet("{studentId:guid}/grades/summary")]
    [HasPermission(PermissionNames.Grades.View, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> GetGradeSummary(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _academics.GetAcademicSummaryAsync(studentId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>The student's full grade history grouped by term (Latest → Oldest).</summary>
    [HttpGet("{studentId:guid}/grades/history")]
    [HasPermission(PermissionNames.Grades.View, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> GetGradeHistory(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _academics.GetStudentSemesterHistoryAsync(studentId, cancellationToken);
        return Ok(result);
    }

    /// <summary>The student's current (actively enrolled) registrations.</summary>
    [HttpGet("{studentId:guid}/registered")]
    [HasPermission(PermissionNames.RegisteredCourses.View, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> GetRegistered(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _registration.GetRegisteredCoursesAsync(studentId, cancellationToken);
        return Ok(result);
    }
}

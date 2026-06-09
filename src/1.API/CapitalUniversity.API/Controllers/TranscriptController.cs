using System.Security.Claims;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.AcademicRecords.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Student-facing transcript endpoints. Builds the requirement-category
/// transcript from synchronized academic outcomes + the active academic plan +
/// registrations, and exports it as a PDF. The caller's <c>studentId</c> is
/// bound from the JWT, so a student can only ever read their own transcript; the
/// service layer re-checks scope and returns <c>null</c> (mapped to 404) for
/// anything out of scope (no existence leak).
/// </summary>
[ApiController]
[Route("api/transcript")]
public class TranscriptController : ControllerBase
{
    private readonly ITranscriptService _service;

    public TranscriptController(ITranscriptService service)
    {
        _service = service;
    }

    /// <summary>The signed-in student's transcript structure.</summary>
    [HttpGet]
    [HasPermission(PermissionNames.Transcript.View)]
    public async Task<IActionResult> GetTranscript(CancellationToken cancellationToken)
    {
        var result = await _service.GetStudentTranscriptAsync(ResolveStudentId(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>The signed-in student's transcript rendered as a downloadable PDF.</summary>
    [HttpGet("pdf")]
    [HasPermission(PermissionNames.Transcript.View)]
    public async Task<IActionResult> GetTranscriptPdf(CancellationToken cancellationToken)
    {
        var result = await _service.GenerateStudentTranscriptPdfAsync(ResolveStudentId(), cancellationToken);
        return result is null
            ? NotFound()
            : File(result.Content, result.ContentType, result.FileName);
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

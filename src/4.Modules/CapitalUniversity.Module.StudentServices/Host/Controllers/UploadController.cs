using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Module.StudentServices.Host.Controllers;

[ApiController]
[Route("api/student-services/upload")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IFileUploadService _fileUploadService;
    private readonly StudentServicesDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IUserScope _userScope;
    private readonly IPermissionManagementService _permissions;

    public UploadController(
        IFileUploadService fileUploadService,
        StudentServicesDbContext context,
        ICurrentUser currentUser,
        IUserScope userScope,
        IPermissionManagementService permissions)
    {
        _fileUploadService = fileUploadService;
        _context = context;
        _currentUser = currentUser;
        _userScope = userScope;
        _permissions = permissions;
    }

    // Mixed access: a student may upload to THEIR OWN request while it is still
    // editable (same statuses SaveStepDataAsync allows); staff need RequestsEditClose.
    // Students hold no permission grants by design (context-scoped model), so the
    // role branch happens here instead of a [HasPermission] attribute.
    [HttpPost("request/{requestId:guid}/step/{stepKey}")]
    public async Task<IActionResult> UploadFile(Guid requestId, string stepKey, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        if (_userScope.IsStudent)
        {
            var request = await GetRequestInfoAsync(requestId, cancellationToken);
            // 404 (not 403) for non-owned ids — no existence leak.
            if (request == null || request.StudentId != _currentUser.Id) return NotFound();
            if (!IsStudentEditable(request.Status))
                return BadRequest("Cannot modify attachments at this status");
        }
        else if (!await StaffHasPermissionAsync(PermissionNames.StudentServices.RequestsEditClose, cancellationToken))
        {
            return Forbid();
        }

        var attachmentId = await _fileUploadService.UploadFileAsync(file, requestId, stepKey, cancellationToken);
        return Ok(new { attachmentId });
    }

    // Mixed access: student → attachments of their own requests only; staff → RequestsView.
    [HttpGet("attachment/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadFile(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await GetAttachmentInfoAsync(attachmentId, cancellationToken);
        if (attachment == null) return NotFound();

        if (_userScope.IsStudent)
        {
            var request = await GetRequestInfoAsync(attachment.StudentRequestId, cancellationToken);
            if (request == null || request.StudentId != _currentUser.Id) return NotFound();
        }
        else if (!await StaffHasPermissionAsync(PermissionNames.StudentServices.RequestsView, cancellationToken))
        {
            return Forbid();
        }

        var fileBytes = await _fileUploadService.DownloadFileAsync(attachmentId, cancellationToken);
        if (fileBytes == null) return NotFound();

        var mimeType = attachment.MimeType ?? "application/octet-stream";
        var fileName = attachment.FileName ?? "file";

        return File(fileBytes, mimeType, fileName);
    }

    // Mixed access: student → may remove a file from their own DRAFT request only;
    // staff → RequestsEditClose.
    [HttpDelete("attachment/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteFile(Guid attachmentId, CancellationToken cancellationToken)
    {
        if (_userScope.IsStudent)
        {
            var attachment = await GetAttachmentInfoAsync(attachmentId, cancellationToken);
            if (attachment == null) return NotFound();

            var request = await GetRequestInfoAsync(attachment.StudentRequestId, cancellationToken);
            if (request == null || request.StudentId != _currentUser.Id) return NotFound();
            if (request.Status != RequestStatus.Draft)
                return BadRequest("Cannot remove attachments at this status");
        }
        else if (!await StaffHasPermissionAsync(PermissionNames.StudentServices.RequestsEditClose, cancellationToken))
        {
            return Forbid();
        }

        await _fileUploadService.DeleteFileAsync(attachmentId, cancellationToken);
        return NoContent();
    }

    // Mirrors the SaveStepDataAsync status guard in StudentRequestService.
    private static bool IsStudentEditable(RequestStatus status) =>
        status is RequestStatus.Draft or RequestStatus.MoreInfoRequired or RequestStatus.PaymentPending;

    private async Task<RequestAttachment?> GetAttachmentInfoAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        return await _context.RequestAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
    }

    private async Task<StudentRequest?> GetRequestInfoAsync(Guid requestId, CancellationToken cancellationToken)
    {
        return await _context.StudentRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
    }

    // Imperative version of the [HasPermission] check (same lookup the
    // PermissionHandler uses). Identities MUST go through PermissionIdentity.Parse —
    // GetPermissionLookupAsync stores canonical (Create-normalized) entries, so the
    // raw kebab-case constants would never match.
    private async Task<bool> StaffHasPermissionAsync(string permission, CancellationToken cancellationToken)
    {
        var grants = await _permissions.GetPermissionLookupAsync(_currentUser.Id, cancellationToken);
        return grants.Contains(PermissionIdentity.Parse(permission));
    }
}

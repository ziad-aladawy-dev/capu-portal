using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;

namespace CapitalUniversity.Module.StudentServices.Host.Controllers;

[ApiController]
[Route("api/student-services/upload")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IFileUploadService _fileUploadService;
    private readonly StudentServicesDbContext _context;

    public UploadController(IFileUploadService fileUploadService, StudentServicesDbContext context)
    {
        _fileUploadService = fileUploadService;
        _context = context;
    }

    [HttpPost("request/{requestId:guid}/step/{stepKey}")]
    public async Task<IActionResult> UploadFile(Guid requestId, string stepKey, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        var attachmentId = await _fileUploadService.UploadFileAsync(file, requestId, stepKey, cancellationToken);
        return Ok(new { attachmentId });
    }

    [HttpGet("attachment/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadFile(Guid attachmentId, CancellationToken cancellationToken)
    {
        var fileBytes = await _fileUploadService.DownloadFileAsync(attachmentId, cancellationToken);
        if (fileBytes == null) return NotFound();

        var attachment = await GetAttachmentInfoAsync(attachmentId, cancellationToken);
        if (attachment == null) return NotFound();

        var mimeType = attachment.MimeType ?? "application/octet-stream";
        var fileName = attachment.FileName ?? "file";

        return File(fileBytes, mimeType, fileName);
    }

    [HttpDelete("attachment/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteFile(Guid attachmentId, CancellationToken cancellationToken)
    {
        await _fileUploadService.DeleteFileAsync(attachmentId, cancellationToken);
        return NoContent();
    }

    private async Task<RequestAttachment?> GetAttachmentInfoAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        return await _context.RequestAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
    }
}
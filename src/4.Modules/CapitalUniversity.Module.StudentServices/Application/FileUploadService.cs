using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Module.StudentServices.Abstractions.Services;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CapitalUniversity.Module.StudentServices.Application;

public class FileUploadService : IFileUploadService
{
    private readonly StudentServicesDbContext _context;
    private readonly string _uploadRootPath;

    public FileUploadService(StudentServicesDbContext context, IConfiguration configuration)
    {
        _context = context;
        _uploadRootPath = configuration["StudentServices:UploadPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "student-services");
        if (!Directory.Exists(_uploadRootPath)) Directory.CreateDirectory(_uploadRootPath);
    }

    public async Task<string> UploadFileAsync(IFormFile file, Guid requestId, string stepKey, CancellationToken cancellationToken = default)
    {
        var request = await _context.StudentRequests.FindAsync(new object[] { requestId }, cancellationToken);
        if (request == null) throw new NotFoundException("Request not found");

        var folderPath = Path.Combine(_uploadRootPath, requestId.ToString(), stepKey);
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        var uniqueFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(folderPath, uniqueFileName);
        var relativePath = Path.Combine("uploads", "student-services", requestId.ToString(), stepKey, uniqueFileName).Replace('\\', '/');

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);

        var attachment = new RequestAttachment
        {
            StudentRequestId = requestId,
            StepKey = stepKey,
            FileName = file.FileName,
            FilePath = relativePath,
            FileSize = file.Length,
            MimeType = file.ContentType
        };

        _context.RequestAttachments.Add(attachment);
        await _context.SaveChangesAsync(cancellationToken);

        return attachment.Id.ToString();
    }

    public async Task<byte[]?> DownloadFileAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _context.RequestAttachments.FindAsync(new object[] { attachmentId }, cancellationToken);
        if (attachment == null) return null;

        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath)) return null;

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public async Task DeleteFileAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _context.RequestAttachments.FindAsync(new object[] { attachmentId }, cancellationToken);
        if (attachment == null) return;

        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FilePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath)) File.Delete(fullPath);

        _context.RequestAttachments.Remove(attachment);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
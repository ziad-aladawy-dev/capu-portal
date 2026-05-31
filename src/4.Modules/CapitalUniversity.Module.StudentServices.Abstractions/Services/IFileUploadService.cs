using Microsoft.AspNetCore.Http;

namespace CapitalUniversity.Module.StudentServices.Abstractions.Services;

public interface IFileUploadService
{
    Task<string> UploadFileAsync(IFormFile file, Guid requestId, string stepKey, CancellationToken cancellationToken = default);
    Task<byte[]?> DownloadFileAsync(Guid attachmentId, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
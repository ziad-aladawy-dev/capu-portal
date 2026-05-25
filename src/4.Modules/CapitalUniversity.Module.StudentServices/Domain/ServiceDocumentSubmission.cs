using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Modules.StudentServices.Domain;

/// <summary>
/// One uploaded document attached to a <see cref="StudentServiceRequest"/>.
/// Stores only the metadata — the binary content lives in the configured
/// file-storage backend; <see cref="FilePath"/> is the opaque storage handle
/// the API layer can resolve back to a stream.
/// </summary>
public class ServiceDocumentSubmission : BaseEntity
{
    public Guid StudentServiceRequestId { get; set; }
    public StudentServiceRequest? StudentServiceRequest { get; set; }

    public Guid DocumentDefinitionId { get; set; }
    public ServiceDocumentDefinition? DocumentDefinition { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>Unique name produced by the storage layer (typically <c>Guid + extension</c>).</summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>Opaque storage handle (local path or blob key). Resolved by the storage layer at download time.</summary>
    public string FilePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }
}

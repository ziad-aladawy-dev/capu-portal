using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Modules.StudentServices.Domain;

/// <summary>
/// One required (or optional) document attached to a <see cref="StudentService"/>.
/// Drives the upload form on the student side and the
/// <c>ServiceDocumentSubmissionValidator</c> on the server side.
/// </summary>
public class ServiceDocumentDefinition : BaseEntity
{
    public Guid StudentServiceId { get; set; }
    public StudentService? StudentService { get; set; }

    /// <summary>Machine name. Unique per service.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bilingual JSON for the rendered label.</summary>
    public string Label { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Comma-separated, lowercase file extensions without leading dot (e.g. <c>"pdf,jpg,png"</c>).</summary>
    public string AllowedExtensions { get; set; } = string.Empty;

    public long MaxFileSizeBytes { get; set; }
}

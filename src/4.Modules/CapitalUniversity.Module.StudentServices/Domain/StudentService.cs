using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Modules.StudentServices.Domain;

/// <summary>
/// A configurable university service (Transcript Request, Enrollment
/// Certificate, …). Owns the configuration shape: dynamic fields, required
/// documents, fee requirements, and the workflow that requests follow.
///
/// <para>
/// <see cref="Name"/> and <see cref="Description"/> are bilingual JSON
/// (<c>{"ar":"…","en":"…"}</c>) — decoded at read time through
/// <c>ILocalizationService</c>. The <see cref="Code"/> is the stable URL slug
/// and never carries translations.
/// </para>
///
/// <para>
/// Allowed processing role ids are stored as a comma-separated list in
/// <see cref="AllowedProcessingRoleIdsCsv"/>. Roles live in the authorization
/// table; this column is a denormalised, by-id reference — the modularity
/// rule forbids EF navigations across module boundaries.
/// </para>
/// </summary>
public class StudentService : BaseEntity, ISoftDeletable
{
    public string Code { get; set; } = string.Empty;

    /// <summary>Bilingual JSON. Localised at read time.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bilingual JSON. Localised at read time. May be empty.</summary>
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public bool RequiresPayment { get; set; }

    /// <summary>Free-text fee classifier passed to the Payments module on invoice creation.</summary>
    public string? FeeType { get; set; }

    public decimal? FeeAmount { get; set; }

    public string Currency { get; set; } = "EGP";

    public int? EstimatedProcessingDays { get; set; }

    /// <summary>Comma-separated role ids. Empty string = the seeded admin role only.</summary>
    public string AllowedProcessingRoleIdsCsv { get; set; } = string.Empty;

    public Guid? WorkflowDefinitionId { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ServiceFieldDefinition> Fields { get; set; } = new List<ServiceFieldDefinition>();
    public ICollection<ServiceDocumentDefinition> Documents { get; set; } = new List<ServiceDocumentDefinition>();
}

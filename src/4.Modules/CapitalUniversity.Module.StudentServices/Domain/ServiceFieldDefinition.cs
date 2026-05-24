using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.StudentServices.Abstractions;

namespace CapitalUniversity.Modules.StudentServices.Domain;

/// <summary>
/// One configurable form field belonging to a <see cref="StudentService"/>.
/// Drives both the rendered submission form and the server-side validation
/// applied to <see cref="ServiceFieldValue"/> rows.
/// </summary>
public class ServiceFieldDefinition : BaseEntity
{
    public Guid StudentServiceId { get; set; }
    public StudentService? StudentService { get; set; }

    /// <summary>Machine name (e.g. <c>"Language"</c>, <c>"NumberOfCopies"</c>). Unique per service.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bilingual JSON for the rendered label.</summary>
    public string Label { get; set; } = string.Empty;

    public DynamicFieldType FieldType { get; set; }

    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }

    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

    /// <summary>Comma-separated allowed dropdown values, in display order. Empty for non-dropdown fields.</summary>
    public string DropdownValues { get; set; } = string.Empty;
}

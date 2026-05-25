using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Modules.StudentServices.Domain;

/// <summary>
/// One submitted value for a <see cref="ServiceFieldDefinition"/> on a
/// specific <see cref="StudentServiceRequest"/>. Stored as a string regardless
/// of <c>FieldDefinition.FieldType</c> — typed coercion is the validator's
/// responsibility at submit time.
/// </summary>
public class ServiceFieldValue : BaseEntity
{
    public Guid StudentServiceRequestId { get; set; }
    public StudentServiceRequest? StudentServiceRequest { get; set; }

    public Guid FieldDefinitionId { get; set; }
    public ServiceFieldDefinition? FieldDefinition { get; set; }

    /// <summary>String representation of the submitted value (number, date, bool serialised consistently with the field type).</summary>
    public string Value { get; set; } = string.Empty;
}

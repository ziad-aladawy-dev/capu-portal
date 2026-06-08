namespace CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;

/// <summary>
/// Full service definition response — used by the admin "view service" path.
/// Carries the configured field + document + workflow shape so the admin UI
/// can render the configuration form without a second round-trip.
/// </summary>
public class StudentServiceResponse
{
    public Guid Id { get; set; }

    /// <summary>Stable code used in URLs / canonical references (e.g. <c>"transcript-request"</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Bilingual JSON — <c>{"ar":"…","en":"…"}</c>. Localised at the API boundary by the consumer.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bilingual JSON. May be empty.</summary>
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    /// <summary>Whether the service raises a fee. Amount/currency come from the Treasury receipt.</summary>
    public bool RequiresPayment { get; set; }

    /// <summary>SLA hint surfaced to students. Service-level — not enforced.</summary>
    public int? EstimatedProcessingDays { get; set; }

    /// <summary>Role ids permitted to process requests of this service.</summary>
    public IReadOnlyList<Guid> AllowedProcessingRoleIds { get; set; } = Array.Empty<Guid>();

    public Guid? WorkflowDefinitionId { get; set; }

    public IReadOnlyList<ServiceFieldDefinitionResponse> Fields { get; set; } = Array.Empty<ServiceFieldDefinitionResponse>();
    public IReadOnlyList<ServiceDocumentDefinitionResponse> Documents { get; set; } = Array.Empty<ServiceDocumentDefinitionResponse>();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Compact listing row. Drops the field/document detail so list queries stay
/// cheap to paginate.
/// </summary>
public class StudentServiceSummaryResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool RequiresPayment { get; set; }
    public int? EstimatedProcessingDays { get; set; }
}

public class ServiceFieldDefinitionResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
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

public class ServiceDocumentDefinitionResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Comma-separated, lowercase file extensions without leading dot (e.g. <c>"pdf,jpg,png"</c>).</summary>
    public string AllowedExtensions { get; set; } = string.Empty;
    public long MaxFileSizeBytes { get; set; }
}

public class CreateStudentServiceRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresPayment { get; set; }
    public int? EstimatedProcessingDays { get; set; }
    public IReadOnlyList<Guid> AllowedProcessingRoleIds { get; set; } = Array.Empty<Guid>();
    public Guid? WorkflowDefinitionId { get; set; }
    public IReadOnlyList<CreateServiceFieldDefinitionRequest> Fields { get; set; } = Array.Empty<CreateServiceFieldDefinitionRequest>();
    public IReadOnlyList<CreateServiceDocumentDefinitionRequest> Documents { get; set; } = Array.Empty<CreateServiceDocumentDefinitionRequest>();
}

/// <summary>
/// PATCH-style sparse update. Only set fields are applied; nested
/// <c>Fields</c> / <c>Documents</c> collections, when supplied, REPLACE the
/// existing configuration wholesale (the admin UI sends the full desired set).
/// </summary>
public class UpdateStudentServiceRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? RequiresPayment { get; set; }
    public int? EstimatedProcessingDays { get; set; }
    public IReadOnlyList<Guid>? AllowedProcessingRoleIds { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public IReadOnlyList<CreateServiceFieldDefinitionRequest>? Fields { get; set; }
    public IReadOnlyList<CreateServiceDocumentDefinitionRequest>? Documents { get; set; }
}

public class CreateServiceFieldDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DynamicFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string DropdownValues { get; set; } = string.Empty;
}

public class CreateServiceDocumentDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
    public string AllowedExtensions { get; set; } = string.Empty;
    public long MaxFileSizeBytes { get; set; }
}

public class ToggleStudentServiceStatusRequest
{
    public bool IsActive { get; set; }
}

using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.StudentServices.Abstractions;

namespace CapitalUniversity.Modules.StudentServices.Domain;

/// <summary>
/// One permitted move between two statuses inside a workflow. Carries the
/// firing mechanism (manual / automatic / student) and, for manual
/// transitions, the permission action verb the caller must hold on the
/// <c>student-services.requests</c> resource.
/// </summary>
public class WorkflowTransition : BaseEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinition? WorkflowDefinition { get; set; }

    public ServiceRequestStatus FromStatus { get; set; }
    public ServiceRequestStatus ToStatus { get; set; }

    public WorkflowTransitionType TransitionType { get; set; } = WorkflowTransitionType.Manual;

    /// <summary>Permission action verb required for manual transitions (e.g. <c>"EditClose"</c>). Empty for student / automatic transitions.</summary>
    public string RequiredAction { get; set; } = string.Empty;
}

using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Modules.StudentServices.Domain;

/// <summary>
/// A reusable workflow definition referenced by zero-or-more
/// <see cref="StudentService"/>s. Owns its set of <see cref="WorkflowState"/>
/// rows (which canonical statuses the workflow supports) and
/// <see cref="WorkflowTransition"/> rows (which moves are permitted, by whom,
/// and how they fire).
/// </summary>
public class WorkflowDefinition : BaseEntity, ISoftDeletable
{
    public string Code { get; set; } = string.Empty;

    /// <summary>Bilingual JSON.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bilingual JSON.</summary>
    public string Description { get; set; } = string.Empty;

    public ICollection<WorkflowState> States { get; set; } = new List<WorkflowState>();
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
}

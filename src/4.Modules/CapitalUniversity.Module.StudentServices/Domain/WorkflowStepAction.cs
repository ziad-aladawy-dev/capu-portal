using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class WorkflowStepAction : BaseEntity
{
    public Guid WorkflowStepId { get; set; }
    public WorkflowStep WorkflowStep { get; set; } = null!;
    public string ActionKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool TriggersSubmission { get; set; }
}
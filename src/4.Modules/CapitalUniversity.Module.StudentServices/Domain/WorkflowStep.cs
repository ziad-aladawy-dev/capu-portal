using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class WorkflowStep : BaseEntity
{
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    public int Order { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public StepInputType InputType { get; set; }
    public bool IsRequired { get; set; }
    public string? ValidationRules { get; set; }

    public ICollection<WorkflowStepAction> AvailableActions { get; set; } = new List<WorkflowStepAction>();
}
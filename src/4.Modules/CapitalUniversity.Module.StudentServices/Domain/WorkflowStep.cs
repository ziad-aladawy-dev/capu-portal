using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class WorkflowStep : BaseEntity
{
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;

    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowStepType StepType { get; set; } = WorkflowStepType.Form;

    public bool IsRequired { get; set; } = true;

    public ICollection<WorkflowStepField> Fields { get; set; } = new List<WorkflowStepField>();
}
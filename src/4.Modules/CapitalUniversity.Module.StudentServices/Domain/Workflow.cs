using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class Workflow : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
}
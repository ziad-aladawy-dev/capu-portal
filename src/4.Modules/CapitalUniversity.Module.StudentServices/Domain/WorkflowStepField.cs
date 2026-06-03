using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

namespace CapitalUniversity.Module.StudentServices.Domain;

public class WorkflowStepField : BaseEntity
{
    public Guid WorkflowStepId { get; set; }
    public WorkflowStep WorkflowStep { get; set; } = null!;

    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;
    public StepFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public string? OptionsJson { get; set; }
}
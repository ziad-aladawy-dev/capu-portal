using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.StudentServices.Abstractions;

namespace CapitalUniversity.Modules.StudentServices.Domain;

/// <summary>
/// One status a request can occupy inside a workflow. Workflows do not invent
/// statuses — they pick which subset of the canonical
/// <see cref="ServiceRequestStatus"/> enum participates and tag each one with
/// per-workflow metadata (display order, initial / terminal flags, whether the
/// state represents "awaiting payment confirmation").
/// </summary>
public class WorkflowState : BaseEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinition? WorkflowDefinition { get; set; }

    public ServiceRequestStatus Status { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsInitial { get; set; }

    public bool IsTerminal { get; set; }

    /// <summary>
    /// True for the state that represents "fee invoice issued, awaiting
    /// payment". <see cref="IStudentServiceRequestService.ConfirmPaymentAsync"/>
    /// moves a request out of any state carrying this flag.
    /// </summary>
    public bool IsWaitingPayment { get; set; }
}

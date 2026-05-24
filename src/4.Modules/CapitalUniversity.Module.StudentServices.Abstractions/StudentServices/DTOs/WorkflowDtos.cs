namespace CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;

public class WorkflowDefinitionResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<WorkflowStateResponse> States { get; set; } = Array.Empty<WorkflowStateResponse>();
    public IReadOnlyList<WorkflowTransitionResponse> Transitions { get; set; } = Array.Empty<WorkflowTransitionResponse>();
}

public class WorkflowStateResponse
{
    public Guid Id { get; set; }
    public ServiceRequestStatus Status { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsInitial { get; set; }
    public bool IsTerminal { get; set; }
    public bool IsWaitingPayment { get; set; }
}

public class WorkflowTransitionResponse
{
    public Guid Id { get; set; }
    public ServiceRequestStatus FromStatus { get; set; }
    public ServiceRequestStatus ToStatus { get; set; }
    public WorkflowTransitionType TransitionType { get; set; }
    /// <summary>Permission action verb that the caller must hold on the <c>student-services.requests</c> resource (e.g. <c>"EditClose"</c>, <c>"Approve"</c>). Empty for student / automatic transitions.</summary>
    public string RequiredAction { get; set; } = string.Empty;
}

public class CreateWorkflowDefinitionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<CreateWorkflowStateRequest> States { get; set; } = Array.Empty<CreateWorkflowStateRequest>();
    public IReadOnlyList<CreateWorkflowTransitionRequest> Transitions { get; set; } = Array.Empty<CreateWorkflowTransitionRequest>();
}

public class CreateWorkflowStateRequest
{
    public ServiceRequestStatus Status { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsInitial { get; set; }
    public bool IsTerminal { get; set; }
    public bool IsWaitingPayment { get; set; }
}

public class CreateWorkflowTransitionRequest
{
    public ServiceRequestStatus FromStatus { get; set; }
    public ServiceRequestStatus ToStatus { get; set; }
    public WorkflowTransitionType TransitionType { get; set; } = WorkflowTransitionType.Manual;
    public string RequiredAction { get; set; } = string.Empty;
}

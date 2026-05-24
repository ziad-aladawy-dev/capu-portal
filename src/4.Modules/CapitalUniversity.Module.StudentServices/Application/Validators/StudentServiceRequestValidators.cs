using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using FluentValidation;

namespace CapitalUniversity.Modules.StudentServices.Application.Validators;

public class SubmitStudentServiceRequestValidator : AbstractValidator<SubmitStudentServiceRequestRequest>
{
    public SubmitStudentServiceRequestValidator()
    {
        RuleFor(x => x.StudentServiceId)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required);

        // Per-field shape — the *semantic* validation against the service's
        // configured field definitions runs in the request service (it needs
        // the loaded definition to coerce types, check min/max, etc.).
        RuleForEach(x => x.FieldValues).ChildRules(child =>
        {
            child.RuleFor(v => v.FieldDefinitionId).NotEmpty();
            child.RuleFor(v => v.Value).MaximumLength(4000);
        });

        RuleForEach(x => x.Files).ChildRules(child =>
        {
            child.RuleFor(f => f.DocumentDefinitionId).NotEmpty();
            child.RuleFor(f => f.FileName).NotEmpty().MaximumLength(256);
            child.RuleFor(f => f.StoredFileName).NotEmpty().MaximumLength(256);
            child.RuleFor(f => f.FilePath).NotEmpty().MaximumLength(1024);
            child.RuleFor(f => f.ContentType).NotEmpty().MaximumLength(128);
            child.RuleFor(f => f.FileSize).GreaterThan(0L);
        });
    }
}

public class CancelStudentServiceRequestValidator : AbstractValidator<CancelStudentServiceRequestRequest>
{
    public CancelStudentServiceRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required)
            .MaximumLength(1024);
    }
}

public class ApproveStudentServiceRequestValidator : AbstractValidator<ApproveStudentServiceRequestRequest>
{
    public ApproveStudentServiceRequestValidator()
    {
        RuleFor(x => x.Note!).MaximumLength(1024).When(x => !string.IsNullOrEmpty(x.Note));
    }
}

public class RejectStudentServiceRequestValidator : AbstractValidator<RejectStudentServiceRequestRequest>
{
    public RejectStudentServiceRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required)
            .MaximumLength(1024);
    }
}

public class MoveRequestWorkflowStateValidator : AbstractValidator<MoveRequestWorkflowStateRequest>
{
    public MoveRequestWorkflowStateValidator()
    {
        RuleFor(x => x.TargetStatus).IsInEnum();
        RuleFor(x => x.Note!).MaximumLength(1024).When(x => !string.IsNullOrEmpty(x.Note));
    }
}

public class CreateWorkflowDefinitionValidator : AbstractValidator<CreateWorkflowDefinitionRequest>
{
    public CreateWorkflowDefinitionValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required)
            .MaximumLength(64)
            .Matches("^[a-z0-9][a-z0-9\\-]*$").WithMessage(LocalizedKeys.Infrastructure.Invalid);

        RuleFor(x => x.Name).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Description).MaximumLength(2048);

        // A workflow must declare at least one state, and exactly one initial
        // state. The "initial" requirement is checked here so the consumer
        // can rely on ResolveInitialStateAsync returning non-null.
        RuleFor(x => x.States)
            .NotEmpty().WithMessage(LocalizedKeys.Infrastructure.Required)
            .Must(states => states.Count(s => s.IsInitial) == 1)
            .WithMessage(LocalizedKeys.StudentServices.WorkflowMissingInitial);

        RuleForEach(x => x.States).ChildRules(state =>
        {
            state.RuleFor(s => s.Status).IsInEnum();
            state.RuleFor(s => s.DisplayOrder).GreaterThanOrEqualTo(0);
        });

        RuleForEach(x => x.Transitions).ChildRules(t =>
        {
            t.RuleFor(x => x.FromStatus).IsInEnum();
            t.RuleFor(x => x.ToStatus).IsInEnum();
            t.RuleFor(x => x.TransitionType).IsInEnum();
            t.RuleFor(x => x.RequiredAction).MaximumLength(64);
        });
    }
}

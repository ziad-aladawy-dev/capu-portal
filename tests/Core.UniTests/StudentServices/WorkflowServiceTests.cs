using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using CapitalUniversity.Modules.StudentServices.Application;
using CapitalUniversity.Modules.StudentServices.Application.Validators;
using CapitalUniversity.Modules.StudentServices.Domain;
using CapitalUniversity.Modules.StudentServices.Repositories;
using FluentAssertions;
using Moq;
using Xunit;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.StudentServices;

/// <summary>
/// WorkflowService owns CRUD over the workflow catalog plus the two resolution
/// paths the request service depends on (initial state + transition lookup).
/// These tests pin every branch: validation/conflict/not-found guards, the
/// state/transition copy on create (including the <c>RequiredAction ?? ""</c>
/// coalesce), DisplayOrder ordering on read, and null fall-through on both
/// resolvers — so conditional / string / null-coalescing mutations are killed.
/// </summary>
public class WorkflowServiceTests
{
    private sealed class Ctx
    {
        public required WorkflowService Sut { get; init; }
        public required Mock<IUnitOfWork> Uow { get; init; }
        public required Mock<IWorkflowRepository> Workflows { get; init; }
    }

    private static Ctx Build()
    {
        var uow = new Mock<IUnitOfWork>();
        var workflows = new Mock<IWorkflowRepository>();
        // Use the real validator so the validation branch is exercised end-to-end.
        var sut = new WorkflowService(uow.Object, workflows.Object, new CreateWorkflowDefinitionValidator());
        return new Ctx { Sut = sut, Uow = uow, Workflows = workflows };
    }

    private static CreateWorkflowStateRequest State(
        ServiceRequestStatus status, int order, bool initial = false, bool terminal = false, bool waiting = false) =>
        new() { Status = status, DisplayOrder = order, IsInitial = initial, IsTerminal = terminal, IsWaitingPayment = waiting };

    private static CreateWorkflowDefinitionRequest ValidCreate() => new()
    {
        Code = "admission",
        Name = "Admission",
        Description = "Admission workflow",
        States = new[]
        {
            State(ServiceRequestStatus.Submitted, 0, initial: true),
            State(ServiceRequestStatus.UnderReview, 1),
            State(ServiceRequestStatus.Approved, 2, terminal: true),
        },
        Transitions = new[]
        {
            new CreateWorkflowTransitionRequest
            {
                FromStatus = ServiceRequestStatus.Submitted,
                ToStatus = ServiceRequestStatus.UnderReview,
                TransitionType = WorkflowTransitionType.Manual,
                RequiredAction = "EditClose",
            },
        },
    };

    private static WorkflowDefinition Definition() => new()
    {
        Code = "admission",
        Name = "{\"ar\":\"قبول\",\"en\":\"Admission\"}",
        Description = "{\"ar\":\"\",\"en\":\"\"}",
        States =
        {
            new WorkflowState { Status = ServiceRequestStatus.Approved, DisplayOrder = 2, IsTerminal = true },
            new WorkflowState { Status = ServiceRequestStatus.Submitted, DisplayOrder = 0, IsInitial = true },
            new WorkflowState { Status = ServiceRequestStatus.UnderReview, DisplayOrder = 1 },
        },
        Transitions =
        {
            new WorkflowTransition
            {
                FromStatus = ServiceRequestStatus.Submitted,
                ToStatus = ServiceRequestStatus.UnderReview,
                TransitionType = WorkflowTransitionType.Manual,
                RequiredAction = "EditClose",
            },
        },
    };

    // ---------------- GetByIdAsync ----------------

    [Fact]
    public async Task GetById_Missing_ReturnsNull()
    {
        var c = Build();
        c.Workflows.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((WorkflowDefinition?)null);

        (await c.Sut.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task GetById_Found_MapsAndOrdersStatesByDisplayOrder()
    {
        var c = Build();
        c.Workflows.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Definition());

        var r = await c.Sut.GetByIdAsync(Guid.NewGuid());

        r.Should().NotBeNull();
        r!.Code.Should().Be("admission");
        r.States.Select(s => s.DisplayOrder).Should().ContainInOrder(0, 1, 2);
        r.States.First().Status.Should().Be(ServiceRequestStatus.Submitted);
        r.States.First().IsInitial.Should().BeTrue();
        r.Transitions.Should().ContainSingle()
            .Which.RequiredAction.Should().Be("EditClose");
    }

    // ---------------- GetAllAsync ----------------

    [Fact]
    public async Task GetAll_MapsEachRow()
    {
        var c = Build();
        c.Workflows.Setup(w => w.GetAllAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new[] { Definition(), Definition() });

        (await c.Sut.GetAllAsync()).Should().HaveCount(2);
    }

    // ---------------- CreateAsync ----------------

    [Fact]
    public async Task Create_Invalid_ThrowsValidation()
    {
        var c = Build();
        var req = ValidCreate();
        req.Code = ""; // fails NotEmpty

        await c.Sut.Invoking(s => s.CreateAsync(req))
               .Should().ThrowAsync<ValidationException>();
        c.Workflows.Verify(w => w.AddAsync(It.IsAny<WorkflowDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_NoInitialState_ThrowsValidation()
    {
        var c = Build();
        var req = ValidCreate();
        req.States = new[] { State(ServiceRequestStatus.Submitted, 0) }; // none IsInitial

        await c.Sut.Invoking(s => s.CreateAsync(req))
               .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Create_DuplicateCode_ThrowsConflict()
    {
        var c = Build();
        c.Workflows.Setup(w => w.ExistsByCodeAsync("admission", null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        await c.Sut.Invoking(s => s.CreateAsync(ValidCreate()))
               .Should().ThrowAsync<ConflictException>()
               .WithMessage(LocalizedKeys.StudentServices.WorkflowCodeInUse);
        c.Workflows.Verify(w => w.AddAsync(It.IsAny<WorkflowDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_Happy_PersistsStatesTransitionsAndReturnsId()
    {
        var c = Build();
        WorkflowDefinition? captured = null;
        c.Workflows.Setup(w => w.ExistsByCodeAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);
        c.Workflows.Setup(w => w.AddAsync(It.IsAny<WorkflowDefinition>(), It.IsAny<CancellationToken>()))
                   .Callback<WorkflowDefinition, CancellationToken>((w, _) => captured = w)
                   .Returns(Task.CompletedTask);

        var id = await c.Sut.CreateAsync(ValidCreate());

        captured.Should().NotBeNull();
        captured!.Code.Should().Be("admission");
        captured.Name.Should().Be(LocalizedJson.Normalize("Admission"));
        captured.States.Should().HaveCount(3);
        captured.States.Single(s => s.IsInitial).Status.Should().Be(ServiceRequestStatus.Submitted);
        captured.Transitions.Should().ContainSingle()
            .Which.RequiredAction.Should().Be("EditClose");
        id.Should().Be(captured.Id);
        c.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_NullRequiredAction_CoalescesToEmpty()
    {
        var c = Build();
        WorkflowDefinition? captured = null;
        var req = ValidCreate();
        req.Transitions = new[]
        {
            new CreateWorkflowTransitionRequest
            {
                FromStatus = ServiceRequestStatus.Submitted,
                ToStatus = ServiceRequestStatus.UnderReview,
                TransitionType = WorkflowTransitionType.Automatic,
                RequiredAction = null!,
            },
        };
        c.Workflows.Setup(w => w.ExistsByCodeAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);
        c.Workflows.Setup(w => w.AddAsync(It.IsAny<WorkflowDefinition>(), It.IsAny<CancellationToken>()))
                   .Callback<WorkflowDefinition, CancellationToken>((w, _) => captured = w)
                   .Returns(Task.CompletedTask);

        await c.Sut.CreateAsync(req);

        captured!.Transitions.Single().RequiredAction.Should().Be(string.Empty);
    }

    // ---------------- DeleteAsync ----------------

    [Fact]
    public async Task Delete_Missing_ThrowsNotFound()
    {
        var c = Build();
        c.Workflows.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((WorkflowDefinition?)null);

        await c.Sut.Invoking(s => s.DeleteAsync(Guid.NewGuid()))
               .Should().ThrowAsync<NotFoundException>()
               .WithMessage(LocalizedKeys.StudentServices.WorkflowNotFound);
        c.Workflows.Verify(w => w.Delete(It.IsAny<WorkflowDefinition>()), Times.Never);
    }

    [Fact]
    public async Task Delete_Happy_DeletesAndSaves()
    {
        var c = Build();
        var def = Definition();
        c.Workflows.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(def);

        await c.Sut.DeleteAsync(def.Id);

        c.Workflows.Verify(w => w.Delete(def), Times.Once);
        c.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------- ResolveTransitionAsync ----------------

    [Fact]
    public async Task ResolveTransition_WorkflowMissing_ReturnsNull()
    {
        var c = Build();
        c.Workflows.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((WorkflowDefinition?)null);

        (await c.Sut.ResolveTransitionAsync(Guid.NewGuid(),
            ServiceRequestStatus.Submitted, ServiceRequestStatus.UnderReview)).Should().BeNull();
    }

    [Fact]
    public async Task ResolveTransition_NoMatch_ReturnsNull()
    {
        var c = Build();
        c.Workflows.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Definition());

        (await c.Sut.ResolveTransitionAsync(Guid.NewGuid(),
            ServiceRequestStatus.UnderReview, ServiceRequestStatus.Approved)).Should().BeNull();
    }

    [Fact]
    public async Task ResolveTransition_Match_ReturnsMapped()
    {
        var c = Build();
        c.Workflows.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Definition());

        var r = await c.Sut.ResolveTransitionAsync(Guid.NewGuid(),
            ServiceRequestStatus.Submitted, ServiceRequestStatus.UnderReview);

        r.Should().NotBeNull();
        r!.FromStatus.Should().Be(ServiceRequestStatus.Submitted);
        r.ToStatus.Should().Be(ServiceRequestStatus.UnderReview);
        r.TransitionType.Should().Be(WorkflowTransitionType.Manual);
        r.RequiredAction.Should().Be("EditClose");
    }

    // ---------------- ResolveInitialStateAsync ----------------

    [Fact]
    public async Task ResolveInitialState_WorkflowMissing_ReturnsNull()
    {
        var c = Build();
        c.Workflows.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((WorkflowDefinition?)null);

        (await c.Sut.ResolveInitialStateAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task ResolveInitialState_NoInitial_ReturnsNull()
    {
        var c = Build();
        var def = Definition();
        foreach (var s in def.States) s.IsInitial = false;
        c.Workflows.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(def);

        (await c.Sut.ResolveInitialStateAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task ResolveInitialState_Found_ReturnsMapped()
    {
        var c = Build();
        c.Workflows.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Definition());

        var r = await c.Sut.ResolveInitialStateAsync(Guid.NewGuid());

        r.Should().NotBeNull();
        r!.Status.Should().Be(ServiceRequestStatus.Submitted);
        r.IsInitial.Should().BeTrue();
    }
}

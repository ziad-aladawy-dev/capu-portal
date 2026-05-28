using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions.DTOs;
using CapitalUniversity.Modules.Schedule.Application;
using CapitalUniversity.Modules.Schedule.Application.Outbox;
using CapitalUniversity.Modules.Schedule.Application.Validators;
using CapitalUniversity.Modules.Schedule.Domain;
using CapitalUniversity.Modules.Schedule.Repositories;
using CapitalUniversity.Core.UniTests._Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.Schedule;

/// <summary>
/// ScheduleSlotService contract: every read/write defers to
/// <c>ICourseOfferingService</c> for parent existence + scope, surfacing
/// out-of-scope as NotFound on the slot key (existence-leak prevention,
/// matching the offering / invoice / academic-plan services). Duplicate
/// (offering, day, start, end) tuples are rejected. Half-open-interval
/// overlap is rejected on (offering, day). Lifecycle events flow through
/// the outbox; conflict-detected facts log synchronously.
/// </summary>
public class ScheduleSlotServiceTests
{
    private static (
        ScheduleSlotService Service,
        Mock<IScheduleSlotRepository> SlotRepo,
        Mock<ICourseOfferingService> Offerings,
        Mock<IUnitOfWork> Uow,
        Mock<IOutbox> Outbox,
        Mock<IAppLogger> Logger)
        Build(CourseOfferingResponse? parentOffering)
    {
        var slotRepo = new Mock<IScheduleSlotRepository>();
        var offerings = new Mock<ICourseOfferingService>();
        var uow = new Mock<IUnitOfWork>();
        
        // Ensure the serializable transaction wrapper actually executes the lambda.
        uow.Setup(u => u.ExecuteInSerializableTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
           .Returns<Func<CancellationToken, Task>, CancellationToken>(async (f, ct) => await f(ct));
           
        var outbox = new Mock<IOutbox>();
        var logger = new Mock<IAppLogger>();

        offerings.Setup(o => o.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(parentOffering);

        var httpContextAccessor = new HttpContextAccessor();
        var sut = new ScheduleSlotService(
            uow.Object,
            slotRepo.Object,
            offerings.Object,
            new ScheduleSlotValidators(
                new CreateScheduleSlotValidator(),
                new UpdateScheduleSlotValidator()),
            outbox.Object,
            logger.Object,
            httpContextAccessor,
            new TestLocalizationService(),
            new Mock<ICacheService>().Object);
        return (sut, slotRepo, offerings, uow, outbox, logger);
    }

    private static CourseOfferingResponse VisibleOffering(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CourseId = Guid.NewGuid(),
        SemesterId = Guid.NewGuid(),
        StructureNodeId = Guid.NewGuid(),
        SectionCode = "A",
        Capacity = 30,
        Status = OfferingStatus.Draft,
        RegistrationState = RegistrationState.Closed,
    };

    private static CreateScheduleSlotRequest ValidCreate(Guid? offeringId = null) => new()
    {
        CourseOfferingId = offeringId ?? Guid.NewGuid(),
        DayOfWeek = DayOfWeek.Monday,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(10, 30),
        Kind = ScheduleSlotKind.Lecture,
    };

    private static ScheduleSlot ExistingSlot(Guid? offeringId = null, DayOfWeek day = DayOfWeek.Monday, int startHour = 9, int endHour = 10)
    {
        var slot = new ScheduleSlot
        {
            Id = Guid.NewGuid(),
            CourseOfferingId = offeringId ?? Guid.NewGuid(),
            DayOfWeek = day,
        };
        slot.SetTimeRange(new TimeOnly(startHour, 0), new TimeOnly(endHour, 0));
        return slot;
    }

    // ----- Existing CRUD + scope contract (unchanged behavior, regression guard) -----

    [Fact]
    public async Task Create_HappyPath_PersistsAndReturnsId()
    {
        var offering = VisibleOffering();
        var (sut, slotRepo, _, uow, _, _) = Build(offering);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

        ScheduleSlot? captured = null;
        slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), It.IsAny<CancellationToken>()))
                .Callback<ScheduleSlot, CancellationToken>((s, _) => captured = s);

        var id = await sut.CreateAsync(ValidCreate(offering.Id));

        id.Should().NotBeEmpty();
        captured.Should().NotBeNull();
        captured!.CourseOfferingId.Should().Be(offering.Id);
        captured.StartTime.Should().Be(new TimeOnly(9, 0));
        captured.EndTime.Should().Be(new TimeOnly(10, 30));
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_OfferingNotVisible_ThrowsNotFound()
    {
        var (sut, _, _, _, _, _) = Build(parentOffering: null);

        var act = () => sut.CreateAsync(ValidCreate());
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_DuplicateTuple_ThrowsConflict()
    {
        var offering = VisibleOffering();
        var (sut, slotRepo, _, _, _, _) = Build(offering);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var act = () => sut.CreateAsync(ValidCreate(offering.Id));
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Create_EndBeforeStart_ThrowsValidation()
    {
        var (sut, _, _, _, _, _) = Build(VisibleOffering());
        var req = ValidCreate();
        req.StartTime = new TimeOnly(12, 0);
        req.EndTime = new TimeOnly(11, 0);

        var act = () => sut.CreateAsync(req);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetById_OfferingNotVisible_ReturnsNull()
    {
        var slot = ExistingSlot();
        var (sut, slotRepo, _, _, _, _) = Build(parentOffering: null);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var result = await sut.GetByIdAsync(slot.Id);
        result.Should().BeNull("an out-of-scope offering must hide its slots silently — no 404, no leak");
    }

    [Fact]
    public async Task GetById_SlotNotFound_ReturnsNull_WithoutCallingParentScope()
    {
        var (sut, slotRepo, offerings, _, _, _) = Build(parentOffering: VisibleOffering());
        slotRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ScheduleSlot?)null);

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
        offerings.Verify(o => o.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "no slot = no parent to scope-check");
    }

    [Fact]
    public async Task GetForOffering_OfferingNotVisible_ReturnsEmpty_WithoutQueryingSlots()
    {
        var (sut, slotRepo, _, _, _, _) = Build(parentOffering: null);

        var result = await sut.GetForOfferingAsync(Guid.NewGuid());

        result.Should().BeEmpty();
        slotRepo.Verify(
            r => r.GetForOfferingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "out-of-scope parent must short-circuit BEFORE the slot repo call");
    }

    [Fact]
    public async Task Update_OutOfScopeOffering_ThrowsNotFound()
    {
        var slot = ExistingSlot();
        var (sut, slotRepo, _, _, _, _) = Build(parentOffering: null);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var act = () => sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { Kind = ScheduleSlotKind.Lab });
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_OnlyEndProvided_ComposesAgainstPersistedStart()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Tuesday, 9, 10);

        var (sut, slotRepo, _, uow, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        slotRepo.Setup(r => r.ExistsAsync(slot.CourseOfferingId, slot.DayOfWeek, It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

        await sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { EndTime = new TimeOnly(11, 30) });

        slot.StartTime.Should().Be(new TimeOnly(9, 0));
        slot.EndTime.Should().Be(new TimeOnly(11, 30));
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_OnlyStartProvided_PastPersistedEnd_ThrowsConflict()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Tuesday, 9, 10);

        var (sut, slotRepo, _, _, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var act = () => sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { StartTime = new TimeOnly(11, 0) });
        await act.Should().ThrowAsync<ConflictException>();
        slot.StartTime.Should().Be(new TimeOnly(9, 0), "rejected update must not leave the entity in a half-mutated state");
        slot.EndTime.Should().Be(new TimeOnly(10, 0));
    }

    [Fact]
    public async Task Update_NoOpOnUniqueTuple_DoesNotRunDuplicateCheck()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id);

        var (sut, slotRepo, _, _, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        await sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { Location = "Hall B" });

        LocalizedJson.Extract(slot.Location, "en").Should().Be("Hall B");
        slotRepo.Verify(
            r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_MovingOntoExistingTuple_ThrowsConflict()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Monday, 9, 10);

        var (sut, slotRepo, _, _, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        slotRepo.Setup(r => r.ExistsAsync(slot.CourseOfferingId, DayOfWeek.Tuesday, new TimeOnly(14, 0), new TimeOnly(15, 0), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var act = () => sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest
        {
            DayOfWeek = DayOfWeek.Tuesday,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0),
        });
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Delete_OutOfScopeOffering_ThrowsNotFound()
    {
        var slot = ExistingSlot();
        var (sut, slotRepo, _, _, _, _) = Build(parentOffering: null);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var act = () => sut.DeleteAsync(slot.Id);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_HappyPath_RemovesAndSaves()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id);
        var (sut, slotRepo, _, uow, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        await sut.DeleteAsync(slot.Id);

        slotRepo.Verify(r => r.Delete(slot), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- Conflict-detection behavior (new) -----

    [Fact]
    public async Task Create_OverlapInSameOfferingAndDay_ThrowsConflict_AndLogsFact()
    {
        var offering = VisibleOffering();
        var (sut, slotRepo, _, uow, outbox, logger) = Build(offering);

        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var act = () => sut.CreateAsync(ValidCreate(offering.Id));
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.Message.Should().Be(LocalizedKeys.Schedule.SlotConflict);

        logger.Verify(
            l => l.LogWarningAsync(
                It.IsAny<string>(),
                nameof(ScheduleSlotService),
                null,
                It.Is<Dictionary<string, object>>(m =>
                    (string)m["MessageType"] == ScheduleSlotService.ScheduleConflictDetectedMessageType
                    && (Guid)m["CourseOfferingId"] == offering.Id)),
            Times.Once);
    }

    [Fact]
    public async Task Create_AdjacentSlot_IsAllowed_NoConflictLogged()
    {
        var offering = VisibleOffering();
        var (sut, slotRepo, _, uow, outbox, logger) = Build(offering);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

        var id = await sut.CreateAsync(ValidCreate(offering.Id));

        id.Should().NotBeEmpty();
        outbox.Verify(o => o.EnqueueAsync(ScheduleSlotCreatedHandler.TypeKey, It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_OverlapWithSelfExcluded_ThrowsConflict()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Monday, 9, 10);

        var (sut, slotRepo, _, uow, outbox, logger) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(slot.CourseOfferingId, DayOfWeek.Monday, new TimeOnly(9, 30), new TimeOnly(10, 30), slot.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var act = () => sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest
        {
            StartTime = new TimeOnly(9, 30),
            EndTime = new TimeOnly(10, 30),
        });
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_SelfShrinkInPlace_DoesNotSelfConflict()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Monday, 9, 10);

        var (sut, slotRepo, _, uow, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(slot.CourseOfferingId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 30), slot.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

        await sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { EndTime = new TimeOnly(9, 30) });

        slot.EndTime.Should().Be(new TimeOnly(9, 30));
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- Event-emission behavior (new) -----

    [Fact]
    public async Task Create_HappyPath_EnqueuesExactlyOneCreatedEvent_WithMinimalPayload()
    {
        var offering = VisibleOffering();
        var (sut, slotRepo, _, _, outbox, _) = Build(offering);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

        ScheduleSlotEventHandler.ScheduleSlotFact? captured = null;
        outbox.Setup(o => o.EnqueueAsync(
                    ScheduleSlotCreatedHandler.TypeKey,
                    It.IsAny<ScheduleSlotEventHandler.ScheduleSlotFact>(),
                    It.IsAny<CancellationToken>()))
              .Callback<string, object, CancellationToken>((_, f, _) => captured = (ScheduleSlotEventHandler.ScheduleSlotFact)f)
              .Returns(Task.CompletedTask);

        var id = await sut.CreateAsync(ValidCreate(offering.Id));

        captured.Should().NotBeNull();
        captured!.ScheduleSlotId.Should().Be(id);
        outbox.Verify(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_HappyPath_EnqueuesExactlyOneUpdatedEvent_WithPostMutationPayload()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Tuesday, 9, 10);

        var (sut, slotRepo, _, _, outbox, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

        ScheduleSlotEventHandler.ScheduleSlotFact? captured = null;
        outbox.Setup(o => o.EnqueueAsync(
                    ScheduleSlotUpdatedHandler.TypeKey,
                    It.IsAny<ScheduleSlotEventHandler.ScheduleSlotFact>(),
                    It.IsAny<CancellationToken>()))
              .Callback<string, object, CancellationToken>((_, f, _) => captured = (ScheduleSlotEventHandler.ScheduleSlotFact)f)
              .Returns(Task.CompletedTask);

        await sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { EndTime = new TimeOnly(11, 30) });

        captured.Should().NotBeNull();
        captured!.ScheduleSlotId.Should().Be(slot.Id);
        outbox.Verify(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_HappyPath_EnqueuesExactlyOneDeletedEvent()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id);
        var (sut, slotRepo, _, _, outbox, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        await sut.DeleteAsync(slot.Id);

        outbox.Verify(
            o => o.EnqueueAsync(ScheduleSlotDeletedHandler.TypeKey, It.IsAny<ScheduleSlotEventHandler.ScheduleSlotFact>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_DuplicateTuple_DoesNotEnqueueAnyEvent()
    {
        var offering = VisibleOffering();
        var (sut, slotRepo, _, _, outbox, _) = Build(offering);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var act = () => sut.CreateAsync(ValidCreate(offering.Id));
        await act.Should().ThrowAsync<ConflictException>();

        outbox.Verify(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

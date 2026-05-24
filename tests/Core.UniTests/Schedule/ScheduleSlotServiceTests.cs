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
        var outbox = new Mock<IOutbox>();
        var logger = new Mock<IAppLogger>();

        offerings.Setup(o => o.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(parentOffering);

        // M1 — ScheduleSlotService.CreateAsync wraps its conflict-check + add +
        // save block in IUnitOfWork.ExecuteInSerializableTransactionAsync. The
        // production impl opens a SERIALIZABLE transaction on relational
        // providers; the mock here just runs the action so the existing
        // behaviour-level assertions still see what the closure did.
        uow.Setup(u => u.ExecuteInSerializableTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
           .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));

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
            // L9 — Schedule now caches GetByIdAsync responses; the mock cache
            // returns null on Get so behaviour falls through to the repo path
            // (matches every existing assertion below).
            Mock.Of<CapitalUniversity.Core.Abstractions.CrossCutting.Caching.ICacheService>());
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
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default))
                .ReturnsAsync(false);

        ScheduleSlot? captured = null;
        slotRepo.Setup(r => r.AddAsync(It.IsAny<ScheduleSlot>(), default))
                .Callback<ScheduleSlot, CancellationToken>((s, _) => captured = s);

        var id = await sut.CreateAsync(ValidCreate(offering.Id));

        id.Should().NotBeEmpty();
        captured.Should().NotBeNull();
        captured!.CourseOfferingId.Should().Be(offering.Id);
        captured.StartTime.Should().Be(new TimeOnly(9, 0));
        captured.EndTime.Should().Be(new TimeOnly(10, 30));
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
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
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default))
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
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);

        var result = await sut.GetByIdAsync(slot.Id);
        result.Should().BeNull("an out-of-scope offering must hide its slots silently — no 404, no leak");
    }

    [Fact]
    public async Task GetById_SlotNotFound_ReturnsNull_WithoutCallingParentScope()
    {
        // Pins the "slot is null" early-return guard. If mutated to
        // "is not null", the next line would call _offerings.GetByIdAsync
        // with slot.CourseOfferingId on a null entity (NullReferenceException).
        // Also verifies the parent lookup is short-circuited so a missing
        // slot doesn't unnecessarily hit the offering service.
        var (sut, slotRepo, offerings, _, _, _) = Build(parentOffering: VisibleOffering());
        slotRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((ScheduleSlot?)null);

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
        offerings.Verify(o => o.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "no slot = no parent to scope-check");
    }

    [Fact]
    public async Task GetForOffering_OfferingNotVisible_ReturnsEmpty_WithoutQueryingSlots()
    {
        // Pins both the parent-null check and the early-return block
        // removal. A mutation that empties the if-body would still call
        // _slots.GetForOfferingAsync and leak any rows it returns.
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
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);

        var act = () => sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { Kind = ScheduleSlotKind.Lab });
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_OnlyEndProvided_ComposesAgainstPersistedStart()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Tuesday, 9, 10);

        var (sut, slotRepo, _, uow, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);
        slotRepo.Setup(r => r.ExistsAsync(slot.CourseOfferingId, slot.DayOfWeek, It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default))
                .ReturnsAsync(false);

        await sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { EndTime = new TimeOnly(11, 30) });

        slot.StartTime.Should().Be(new TimeOnly(9, 0));
        slot.EndTime.Should().Be(new TimeOnly(11, 30));
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Update_OnlyStartProvided_PastPersistedEnd_ThrowsConflict()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Tuesday, 9, 10);

        var (sut, slotRepo, _, _, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);

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
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);

        await sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { Location = "Hall B" });

        slot.Location.Should().Be("{\"ar\":\"Hall B\",\"en\":\"Hall B\"}");
        slotRepo.Verify(
            r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default),
            Times.Never,
            "the duplicate guard must only run when the unique-index tuple actually moves");
        slotRepo.Verify(
            r => r.HasConflictAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<Guid?>(), default),
            Times.Never,
            "the overlap guard must also be skipped — a no-op tuple cannot conflict with itself or anyone else");
    }

    [Fact]
    public async Task Update_MovingOntoExistingTuple_ThrowsConflict()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Monday, 9, 10);

        var (sut, slotRepo, _, _, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);
        slotRepo.Setup(r => r.ExistsAsync(slot.CourseOfferingId, DayOfWeek.Tuesday, new TimeOnly(14, 0), new TimeOnly(15, 0), default))
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
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);

        var act = () => sut.DeleteAsync(slot.Id);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_HappyPath_RemovesAndSaves()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id);
        var (sut, slotRepo, _, uow, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);

        await sut.DeleteAsync(slot.Id);

        slotRepo.Verify(r => r.Delete(slot), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // ----- Conflict-detection behavior (new) -----

    [Fact]
    public async Task Create_OverlapInSameOfferingAndDay_ThrowsConflict_AndLogsFact()
    {
        var offering = VisibleOffering();
        var (sut, slotRepo, _, uow, outbox, logger) = Build(offering);

        // Tuple-exact duplicate check returns false — this is a partial overlap,
        // not a duplicate row. The overlap check returns true.
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), null, default))
                .ReturnsAsync(true);

        var act = () => sut.CreateAsync(ValidCreate(offering.Id));
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.Message.Should().Be(LocalizedKeys.Schedule.SlotConflict);

        // Conflict-detected fact is observed via the logger (the outbox would
        // roll back with the txn — that's why this path uses synchronous
        // logging, see ScheduleSlotService.LogConflictAsync).
        logger.Verify(
            l => l.LogWarningAsync(
                It.IsAny<string>(),
                nameof(ScheduleSlotService),
                null,
                It.Is<Dictionary<string, object>>(m =>
                    (string)m["MessageType"] == ScheduleSlotService.ScheduleConflictDetectedMessageType
                    && (Guid)m["CourseOfferingId"] == offering.Id)),
            Times.Once,
            "conflict-detected fact must be emitted exactly once via IAppLogger when the overlap check rejects");

        // Nothing was committed; no lifecycle event must be enqueued.
        outbox.Verify(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), default), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Create_AdjacentSlot_IsAllowed_NoConflictLogged()
    {
        // Adjacency = strict-inequality boundary. The overlap predicate uses
        // existing.End > new.Start AND new.End > existing.Start, so a slot
        // ending exactly when the new one starts is permitted.
        var offering = VisibleOffering();
        var (sut, slotRepo, _, uow, outbox, logger) = Build(offering);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), null, default))
                .ReturnsAsync(false);

        var id = await sut.CreateAsync(ValidCreate(offering.Id));

        id.Should().NotBeEmpty();
        logger.Verify(
            l => l.LogWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Microsoft.AspNetCore.Http.HttpContext?>(), It.IsAny<Dictionary<string, object>?>()),
            Times.Never,
            "no conflict was detected → no conflict event must be emitted");
        outbox.Verify(o => o.EnqueueAsync(ScheduleSlotCreatedHandler.TypeKey, It.IsAny<It.IsAnyType>(), default), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Update_OverlapWithSelfExcluded_ThrowsConflict()
    {
        // A genuine overlap with a different row must reject — the
        // excludeId on the repo call must NOT mask a real collision; it
        // only ignores this row's own footprint.
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Monday, 9, 10);

        var (sut, slotRepo, _, uow, outbox, logger) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(slot.CourseOfferingId, DayOfWeek.Monday, new TimeOnly(9, 30), new TimeOnly(10, 30), slot.Id, default))
                .ReturnsAsync(true);

        var act = () => sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest
        {
            StartTime = new TimeOnly(9, 30),
            EndTime = new TimeOnly(10, 30),
        });
        await act.Should().ThrowAsync<ConflictException>();

        logger.Verify(
            l => l.LogWarningAsync(It.IsAny<string>(), nameof(ScheduleSlotService), null, It.IsAny<Dictionary<string, object>?>()),
            Times.Once);
        outbox.Verify(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), default), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Update_SelfShrinkInPlace_DoesNotSelfConflict()
    {
        // The excludeId guarantees that a row narrowing itself (9-10 → 9-9:30)
        // does not get rejected by colliding with its own previous footprint.
        // The repo mock is set up to return false ONLY when slot.Id is
        // excluded — without the excludeId pass-through, the test would fail.
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Monday, 9, 10);

        var (sut, slotRepo, _, uow, _, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(slot.CourseOfferingId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 30), slot.Id, default))
                .ReturnsAsync(false);

        await sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { EndTime = new TimeOnly(9, 30) });

        slot.EndTime.Should().Be(new TimeOnly(9, 30));
        slotRepo.Verify(
            r => r.HasConflictAsync(slot.CourseOfferingId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(9, 30), slot.Id, default),
            Times.Once,
            "the service must pass the slot's own id as excludeId so self-overlap is impossible");
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // ----- Event-emission behavior (new) -----

    [Fact]
    public async Task Create_HappyPath_EnqueuesExactlyOneCreatedEvent_WithMinimalPayload()
    {
        var offering = VisibleOffering();
        var (sut, slotRepo, _, _, outbox, _) = Build(offering);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<Guid?>(), default))
                .ReturnsAsync(false);

        ScheduleSlotEventHandler.ScheduleSlotFact? captured = null;
        outbox.Setup(o => o.EnqueueAsync(
                    ScheduleSlotCreatedHandler.TypeKey,
                    It.IsAny<ScheduleSlotEventHandler.ScheduleSlotFact>(),
                    default))
              .Callback<string, ScheduleSlotEventHandler.ScheduleSlotFact, CancellationToken>((_, f, _) => captured = f)
              .Returns(Task.CompletedTask);

        var id = await sut.CreateAsync(ValidCreate(offering.Id));

        captured.Should().NotBeNull();
        captured!.ScheduleSlotId.Should().Be(id);
        captured.CourseOfferingId.Should().Be(offering.Id);
        captured.DayOfWeek.Should().Be(DayOfWeek.Monday);
        captured.StartTime.Should().Be(new TimeOnly(9, 0));
        captured.EndTime.Should().Be(new TimeOnly(10, 30));
        captured.Kind.Should().Be(ScheduleSlotKind.Lecture);

        // Exactly one event — not zero, not a stray duplicate.
        outbox.Verify(
            o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), default),
            Times.Once);
        // It must be the Created discriminator, not Updated or Deleted.
        outbox.Verify(o => o.EnqueueAsync(ScheduleSlotUpdatedHandler.TypeKey, It.IsAny<It.IsAnyType>(), default), Times.Never);
        outbox.Verify(o => o.EnqueueAsync(ScheduleSlotDeletedHandler.TypeKey, It.IsAny<It.IsAnyType>(), default), Times.Never);
    }

    [Fact]
    public async Task Update_HappyPath_EnqueuesExactlyOneUpdatedEvent_WithPostMutationPayload()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id, DayOfWeek.Tuesday, 9, 10);

        var (sut, slotRepo, _, _, outbox, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default))
                .ReturnsAsync(false);
        slotRepo.Setup(r => r.HasConflictAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<Guid?>(), default))
                .ReturnsAsync(false);

        ScheduleSlotEventHandler.ScheduleSlotFact? captured = null;
        outbox.Setup(o => o.EnqueueAsync(
                    ScheduleSlotUpdatedHandler.TypeKey,
                    It.IsAny<ScheduleSlotEventHandler.ScheduleSlotFact>(),
                    default))
              .Callback<string, ScheduleSlotEventHandler.ScheduleSlotFact, CancellationToken>((_, f, _) => captured = f)
              .Returns(Task.CompletedTask);

        await sut.UpdateAsync(slot.Id, new UpdateScheduleSlotRequest { EndTime = new TimeOnly(11, 30) });

        captured.Should().NotBeNull();
        captured!.ScheduleSlotId.Should().Be(slot.Id);
        captured.EndTime.Should().Be(new TimeOnly(11, 30), "the event must carry the post-mutation state, not the pre-mutation snapshot");
        outbox.Verify(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), default), Times.Once);
        outbox.Verify(o => o.EnqueueAsync(ScheduleSlotCreatedHandler.TypeKey, It.IsAny<It.IsAnyType>(), default), Times.Never);
    }

    [Fact]
    public async Task Delete_HappyPath_EnqueuesExactlyOneDeletedEvent()
    {
        var offering = VisibleOffering();
        var slot = ExistingSlot(offering.Id);
        var (sut, slotRepo, _, _, outbox, _) = Build(offering);
        slotRepo.Setup(r => r.GetByIdAsync(slot.Id, default)).ReturnsAsync(slot);

        await sut.DeleteAsync(slot.Id);

        outbox.Verify(
            o => o.EnqueueAsync(ScheduleSlotDeletedHandler.TypeKey, It.IsAny<ScheduleSlotEventHandler.ScheduleSlotFact>(), default),
            Times.Once);
        outbox.Verify(o => o.EnqueueAsync(ScheduleSlotCreatedHandler.TypeKey, It.IsAny<It.IsAnyType>(), default), Times.Never);
        outbox.Verify(o => o.EnqueueAsync(ScheduleSlotUpdatedHandler.TypeKey, It.IsAny<It.IsAnyType>(), default), Times.Never);
    }

    [Fact]
    public async Task Create_DuplicateTuple_DoesNotEnqueueAnyEvent()
    {
        // Failed precondition → no lifecycle event. The outbox is staged on
        // the DbContext, so if we wrote the row then threw we'd rely on the
        // SaveChanges-never-happened path. Defence in depth: do not enqueue.
        var offering = VisibleOffering();
        var (sut, slotRepo, _, _, outbox, _) = Build(offering);
        slotRepo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<DayOfWeek>(), It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), default))
                .ReturnsAsync(true);

        var act = () => sut.CreateAsync(ValidCreate(offering.Id));
        await act.Should().ThrowAsync<ConflictException>();

        outbox.Verify(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), default), Times.Never);
    }
}

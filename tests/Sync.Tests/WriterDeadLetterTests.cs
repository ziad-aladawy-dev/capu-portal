using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Modules.Registration.Abstractions;
using CapitalUniversity.Modules.Registration.Domain;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Domain;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Enums;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Registration.Pull;
using CapitalUniversity.Sync.Schedules.Pull;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

// `Student` / `Course` are namespace segments under CapitalUniversity.Sync.* —
// they shadow the Core entity types when this assembly resolves the bare name.
// Alias to the Core entities for the gateway's generic resolve setups.
using CoreStudent = CapitalUniversity.Core.Domain.Identity.Student;
using CoreCourse = CapitalUniversity.Core.Domain.Courses.Course;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Pins the dead-letter behaviour added to the pull writers that resolve an
/// externally-sourced FK before persisting (Registration → Student/Course,
/// Schedules → CourseOffering). When the dependency isn't in Core yet the row
/// must be recorded to <see cref="IFailureRepository"/> as an
/// <c>UnresolvedDependency</c> — carrying the run's CorrelationId/Attempt — and
/// skipped, instead of being silently dropped once the cursor advances. Rows
/// whose dependencies all resolve must persist with NO dead-letter.
/// </summary>
public class WriterDeadLetterTests
{
    private static readonly Guid Corr = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const int Attempt = 3;

    // ── Registration ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Registration_UnresolvedStudent_DeadLettersAndSkips()
    {
        var gateway = new Mock<ICoreWriteGateway>();
        gateway.Setup(g => g.ResolveIdByExternalIdAsync<CoreStudent>("EXT-S-MISSING", It.IsAny<CancellationToken>()))
               .ReturnsAsync((Guid?)null);
        var failures = new Mock<IFailureRepository>();

        var sut = new RegistrationWriter(
            gateway.Object, failures.Object, RunContext(), NullLogger<RegistrationWriter>.Instance);

        var result = await sut.UpsertBatchAsync(
            new[] { RegistrationDispatch("EXT-REG-1", "EXT-S-MISSING", "EXT-C-1") }, CancellationToken.None);

        result.Should().Be(0, "the unresolved row is the only one in the batch");

        failures.Verify(f => f.RecordAsync(
            It.Is<SyncFailureRecord>(r =>
                r.ErrorType == "UnresolvedDependency" &&
                r.CorrelationId == Corr &&
                r.Attempt == Attempt &&
                r.ErrorMessage.Contains("EXT-REG-1") &&
                r.ErrorMessage.Contains("EXT-S-MISSING")),
            It.IsAny<CancellationToken>()), Times.Once);

        // Nothing reached Core — the gateway upsert is never called for a batch
        // with no resolvable rows.
        gateway.Verify(g => g.UpsertAsync(
            It.IsAny<IReadOnlyList<StudentRegisteredCourse>>(),
            It.IsAny<Action<StudentRegisteredCourse, StudentRegisteredCourse>>(),
            It.IsAny<CoreUpsertOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Registration_UnresolvedCourse_DeadLettersWithCourseReason()
    {
        var gateway = new Mock<ICoreWriteGateway>();
        gateway.Setup(g => g.ResolveIdByExternalIdAsync<CoreStudent>("EXT-S-1", It.IsAny<CancellationToken>()))
               .ReturnsAsync((Guid?)Guid.NewGuid());
        gateway.Setup(g => g.ResolveIdByExternalIdAsync<CoreCourse>("EXT-C-MISSING", It.IsAny<CancellationToken>()))
               .ReturnsAsync((Guid?)null);
        var failures = new Mock<IFailureRepository>();

        var sut = new RegistrationWriter(
            gateway.Object, failures.Object, RunContext(), NullLogger<RegistrationWriter>.Instance);

        var result = await sut.UpsertBatchAsync(
            new[] { RegistrationDispatch("EXT-REG-2", "EXT-S-1", "EXT-C-MISSING") }, CancellationToken.None);

        result.Should().Be(0);
        failures.Verify(f => f.RecordAsync(
            It.Is<SyncFailureRecord>(r =>
                r.ErrorType == "UnresolvedDependency" &&
                r.ErrorMessage.Contains("EXT-C-MISSING") &&
                r.ErrorMessage.Contains("course")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Registration_AllResolved_PersistsWithoutDeadLetter()
    {
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var gateway = new Mock<ICoreWriteGateway>();
        gateway.Setup(g => g.ResolveIdByExternalIdAsync<CoreStudent>("EXT-S-1", It.IsAny<CancellationToken>()))
               .ReturnsAsync((Guid?)studentId);
        gateway.Setup(g => g.ResolveIdByExternalIdAsync<CoreCourse>("EXT-C-1", It.IsAny<CancellationToken>()))
               .ReturnsAsync((Guid?)courseId);

        IReadOnlyList<StudentRegisteredCourse>? captured = null;
        gateway.Setup(g => g.UpsertAsync(
                It.IsAny<IReadOnlyList<StudentRegisteredCourse>>(),
                It.IsAny<Action<StudentRegisteredCourse, StudentRegisteredCourse>>(),
                It.IsAny<CoreUpsertOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<StudentRegisteredCourse>, Action<StudentRegisteredCourse, StudentRegisteredCourse>, CoreUpsertOptions, CancellationToken>(
                (batch, _, _, _) => captured = batch)
            .ReturnsAsync(new CoreUpsertResult { Persisted = 1 });

        var failures = new Mock<IFailureRepository>(MockBehavior.Strict); // any RecordAsync call would throw

        var sut = new RegistrationWriter(
            gateway.Object, failures.Object, RunContext(), NullLogger<RegistrationWriter>.Instance);

        var result = await sut.UpsertBatchAsync(
            new[] { RegistrationDispatch("EXT-REG-3", "EXT-S-1", "EXT-C-1") }, CancellationToken.None);

        result.Should().Be(1);
        captured.Should().NotBeNull();
        captured![0].StudentId.Should().Be(studentId, "the writer assigns the resolved Core student id");
        captured[0].CourseId.Should().Be(courseId, "the writer assigns the resolved Core course id");
        failures.VerifyNoOtherCalls();
    }

    // ── Schedules (same pattern, single FK) ──────────────────────────────────────

    [Fact]
    public async Task Schedule_UnresolvedOffering_DeadLettersAndSkips()
    {
        var gateway = new Mock<ICoreWriteGateway>();
        gateway.Setup(g => g.ResolveIdByExternalIdAsync<CourseOfferingEntity>("EXT-CO-MISSING", It.IsAny<CancellationToken>()))
               .ReturnsAsync((Guid?)null);
        var failures = new Mock<IFailureRepository>();

        var sut = new ScheduleSlotWriter(
            gateway.Object, failures.Object, RunContext(), NullLogger<ScheduleSlotWriter>.Instance);

        var result = await sut.UpsertBatchAsync(new[] { ScheduleDispatch("EXT-SLOT-1", "EXT-CO-MISSING") }, CancellationToken.None);

        result.Should().Be(0);
        failures.Verify(f => f.RecordAsync(
            It.Is<SyncFailureRecord>(r =>
                r.ErrorType == "UnresolvedDependency" &&
                r.CorrelationId == Corr &&
                r.Attempt == Attempt &&
                r.ErrorMessage.Contains("EXT-SLOT-1") &&
                r.ErrorMessage.Contains("EXT-CO-MISSING")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static StubRunContext RunContext() => new()
    {
        Current = new SyncContext
        {
            ModuleName = "test",
            Direction = SyncDirection.Pull,
            Metadata = new SyncRunMetadata { CorrelationId = Corr, TriggeredBy = "test" },
            Attempt = Attempt,
        }
    };

    private static RegistrationSyncDispatch RegistrationDispatch(string regId, string studentId, string courseId) => new()
    {
        Entity = new StudentRegisteredCourse
        {
            ExternallySourced = new() { ExternalId = regId },
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            AttemptNumber = 1,
            RegistrationStatus = RegistrationStatus.Enrolled,
            RegisteredAt = new DateTime(2026, 1, 1),
        },
        ExternalStudentId = studentId,
        ExternalCourseId = courseId,
    };

    private static ScheduleSlotSyncDispatch ScheduleDispatch(string slotId, string offeringId)
    {
        var slot = new ScheduleSlot
        {
            ExternallySourced = new() { ExternalId = slotId },
            CourseOfferingId = Guid.Empty,
            DayOfWeek = DayOfWeek.Monday,
            Kind = ScheduleSlotKind.Lecture,
        };
        slot.SetTimeRange(new TimeOnly(9, 0), new TimeOnly(10, 0));
        return new ScheduleSlotSyncDispatch { Entity = slot, ExternalCourseOfferingId = offeringId };
    }

    private sealed class StubRunContext : ISyncRunContextAccessor
    {
        public SyncContext? Current { get; init; }
    }
}

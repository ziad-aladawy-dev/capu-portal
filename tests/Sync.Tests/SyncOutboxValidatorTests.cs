using FluentAssertions;
using Xunit;

using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions;

using StaffPayload = CapitalUniversity.Sync.Staff.Domain.ExternalStaff;
using StaffRow = CapitalUniversity.Sync.Staff.Push.StaffOutboxEntity;
using StaffDispatch = CapitalUniversity.Sync.Staff.Push.StaffOutboxDispatch;
using StaffOutboxValidator = CapitalUniversity.Sync.Staff.Push.StaffOutboxValidator;

using StudentPayload = CapitalUniversity.Sync.Student.Domain.ExternalStudent;
using StudentRow = CapitalUniversity.Sync.Student.Push.StudentOutboxEntity;
using StudentDispatch = CapitalUniversity.Sync.Student.Push.StudentOutboxDispatch;
using StudentOutboxValidator = CapitalUniversity.Sync.Student.Push.StudentOutboxValidator;

using CoursePayload = CapitalUniversity.Sync.Courses.Domain.ExternalCourse;
using CourseRow = CapitalUniversity.Sync.Courses.Push.CourseOutboxEntity;
using CourseDispatch = CapitalUniversity.Sync.Courses.Push.CourseOutboxDispatch;
using CourseOutboxValidator = CapitalUniversity.Sync.Courses.Push.CourseOutboxValidator;


using SlotPayload = CapitalUniversity.Sync.Schedules.Domain.ExternalScheduleSlot;
using SlotRow = CapitalUniversity.Sync.Schedules.Push.ScheduleSlotOutboxEntity;
using SlotDispatch = CapitalUniversity.Sync.Schedules.Push.ScheduleSlotOutboxDispatch;
using ScheduleSlotOutboxValidator = CapitalUniversity.Sync.Schedules.Push.ScheduleSlotOutboxValidator;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Branch-complete coverage for the outbound (Push) outbox validators. Each
/// validator guards two invariants — payload merge-key present, and row/payload
/// merge-key agreement (Ordinal) — plus a happy path. Exact error strings are
/// asserted to kill string mutations.
/// </summary>
public class SyncOutboxValidatorTests
{
    private static StaffPayload StaffPayloadWith(string id) => new()
    {
        ExternalStaffId = id,
        EmployeeCode = "E1",
        Name = "N",
        NationalId = "1",
        BirthDate = new DateTime(1990, 1, 1),
        PhoneNumber = "0",
        Email = "e@x.com",
        Role = "R",
        JobTitle = "J",
        IsActive = true,
        ExternalUpdatedAt = DateTimeOffset.UnixEpoch,
        ExternalVersion = 1,
    };

    [Fact]
    public void Staff_Valid_ReturnsTrue()
    {
        var d = new StaffDispatch { Row = new StaffRow { ExternalStaffId = "A" }, Payload = StaffPayloadWith("A") };
        new StaffOutboxValidator().IsValid(d, out var err).Should().BeTrue();
        err.Should().BeNull();
    }

    [Fact]
    public void Staff_BlankPayloadId_Fails()
    {
        var d = new StaffDispatch { Row = new StaffRow { ExternalStaffId = "A" }, Payload = StaffPayloadWith("") };
        new StaffOutboxValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("Outbox payload missing ExternalStaffId.");
    }

    [Fact]
    public void Staff_Mismatch_Fails()
    {
        var d = new StaffDispatch { Row = new StaffRow { ExternalStaffId = "B" }, Payload = StaffPayloadWith("A") };
        new StaffOutboxValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("Outbox payload ExternalStaffId mismatch.");
    }

    private static StudentPayload StudentPayloadWith(string id) => new()
    {
        ExternalStudentId = id,
        StudentCode = "S1",
        Name = "N",
        NationalId = "1",
        BirthDate = new DateTime(2000, 1, 1),
        PhoneNumber = "0",
        Email = "e@x.com",
        IsActive = true,
        ExternalUpdatedAt = DateTimeOffset.UnixEpoch,
        ExternalVersion = 1,
    };

    [Fact]
    public void Student_Valid_ReturnsTrue()
    {
        var d = new StudentDispatch { Row = new StudentRow { ExternalStudentId = "A" }, Payload = StudentPayloadWith("A") };
        new StudentOutboxValidator().IsValid(d, out var err).Should().BeTrue();
        err.Should().BeNull();
    }

    [Fact]
    public void Student_BlankPayloadId_Fails()
    {
        var d = new StudentDispatch { Row = new StudentRow { ExternalStudentId = "A" }, Payload = StudentPayloadWith("  ") };
        new StudentOutboxValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("Outbox payload missing ExternalStudentId.");
    }

    [Fact]
    public void Student_Mismatch_Fails()
    {
        var d = new StudentDispatch { Row = new StudentRow { ExternalStudentId = "a" }, Payload = StudentPayloadWith("A") };
        new StudentOutboxValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("Outbox payload ExternalStudentId mismatch.");
    }

    private static CoursePayload CoursePayloadWith(string id) => new()
    {
        ExternalCourseId = id,
        Code = "C1",
        Title = "T",
        CreditHours = 3,
        Category = CourseCategory.Unspecified,
        IsActive = true,
        ExternalUpdatedAt = DateTimeOffset.UnixEpoch,
        ExternalVersion = 1,
    };

    [Fact]
    public void Course_Valid_ReturnsTrue()
    {
        var d = new CourseDispatch { Row = new CourseRow { ExternalCourseId = "A" }, Payload = CoursePayloadWith("A") };
        new CourseOutboxValidator().IsValid(d, out var err).Should().BeTrue();
        err.Should().BeNull();
    }

    [Fact]
    public void Course_BlankPayloadId_Fails()
    {
        var d = new CourseDispatch { Row = new CourseRow { ExternalCourseId = "A" }, Payload = CoursePayloadWith("") };
        new CourseOutboxValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("Outbox payload missing ExternalCourseId.");
    }

    [Fact]
    public void Course_Mismatch_Fails()
    {
        var d = new CourseDispatch { Row = new CourseRow { ExternalCourseId = "X" }, Payload = CoursePayloadWith("A") };
        new CourseOutboxValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("Outbox payload ExternalCourseId mismatch.");
    }

    private static SlotPayload SlotPayloadWith(string id) => new()
    {
        ExternalScheduleSlotId = id,
        ExternalCourseOfferingId = "OFF-1",
        DayOfWeek = DayOfWeek.Monday,
        StartTime = new TimeOnly(8, 0),
        EndTime = new TimeOnly(10, 0),
        Kind = ScheduleSlotKind.Lecture,
        ExternalUpdatedAt = DateTimeOffset.UnixEpoch,
        ExternalVersion = 1,
    };

    [Fact]
    public void ScheduleSlot_Valid_ReturnsTrue()
    {
        var d = new SlotDispatch { Row = new SlotRow { ExternalScheduleSlotId = "A" }, Payload = SlotPayloadWith("A") };
        new ScheduleSlotOutboxValidator().IsValid(d, out var err).Should().BeTrue();
        err.Should().BeNull();
    }

    [Fact]
    public void ScheduleSlot_BlankPayloadId_Fails()
    {
        var d = new SlotDispatch { Row = new SlotRow { ExternalScheduleSlotId = "A" }, Payload = SlotPayloadWith("") };
        new ScheduleSlotOutboxValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("Outbox payload missing ExternalScheduleSlotId.");
    }

    [Fact]
    public void ScheduleSlot_Mismatch_Fails()
    {
        var d = new SlotDispatch { Row = new SlotRow { ExternalScheduleSlotId = "Q" }, Payload = SlotPayloadWith("A") };
        new ScheduleSlotOutboxValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("Outbox payload ExternalScheduleSlotId mismatch.");
    }
}
